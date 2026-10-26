using Obol.Library.Enums;
using Obol.WebApi.Features.Portfolio.Models.Entities;

namespace Obol.WebApi.Features.Portfolio.Services;

/// <summary>A dividend expected from a payment of the last twelve months.</summary>
/// <param name="InstrumentId">The instrument.</param>
/// <param name="Day">The expected day (a year after last year's).</param>
/// <param name="Amount">The expected amount, in the account currency.</param>
internal sealed record ExpectedPayment(int InstrumentId, DateOnly Day, decimal Amount);

/// <summary>A payment received: a dividend or interest.</summary>
/// <param name="InstrumentId">The instrument (null for interest).</param>
/// <param name="Day">The local day.</param>
/// <param name="Amount">The amount, in the account currency.</param>
internal sealed record IncomePayment(int? InstrumentId, DateOnly Day, decimal Amount);

/// <summary>
/// Dividends and interest: what came in, and what the positions held now should pay over the next twelve months if every
/// payment of the last twelve repeats (per share, on today's shares).
/// </summary>
internal static class IncomeMath
{
    /// <summary>
    /// Every dividend and interest payment, by local day.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <returns>The payments, oldest first.</returns>
    public static List<IncomePayment> Received(PortfolioData data) =>
        data.Dividends.Select(dividend => new IncomePayment(dividend.InstrumentID, data.LocalDay(dividend.PaidAt), dividend.Amount))
            .Concat(data.Cash.Where(static movement => movement.Kind == (byte)CashKind.Interest)
                .Select(movement => new IncomePayment(null, data.LocalDay(movement.At), movement.Amount)))
            .OrderBy(static payment => payment.Day)
            .ToList();

    /// <summary>
    /// The payments expected over the next twelve months.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <param name="positions">The positions now.</param>
    /// <returns>The expected payments, soonest first.</returns>
    public static List<ExpectedPayment> Expected(PortfolioData data, IReadOnlyList<Position> positions)
    {
        var held = positions.ToDictionary(static position => position.Instrument.InstrumentID, static position => position.Quantity);
        var since = data.Today.AddYears(-1);
        var expected = new List<ExpectedPayment>();
        foreach (var dividend in data.Dividends)
        {
            var day = data.LocalDay(dividend.PaidAt);
            if (day <= since || day > data.Today || !held.TryGetValue(dividend.InstrumentID, out var sharesNow) || sharesNow <= 0)
            {
                continue;
            }

            var sharesThen = dividend.Quantity is > 0 ? dividend.Quantity.Value : PortfolioMath.QuantityOn(data, dividend.InstrumentID, day);
            if (sharesThen <= 0)
            {
                continue;
            }

            var next = day.AddYears(1);
            if (next <= data.Today)
            {
                next = data.Today.AddDays(1);
            }

            expected.Add(new ExpectedPayment(dividend.InstrumentID, next, Math.Round(dividend.Amount / sharesThen * sharesNow, 2)));
        }

        return expected.OrderBy(static payment => payment.Day).ToList();
    }

    /// <summary>
    /// The income of every instrument that paid in the last 12 months or is held and has paid before: what it paid,
    /// its yield, whether the last payment per share went up or down, and the tax withheld at source.
    /// </summary>
    /// <param name="data">The account.</param>
    /// <param name="positions">The positions now.</param>
    /// <returns>The income by instrument, biggest first.</returns>
    public static List<IncomeByPosition> ByPosition(PortfolioData data, IReadOnlyList<Position> positions)
    {
        var held = positions.ToDictionary(static position => position.Instrument.InstrumentID);
        var since = data.Today.AddYears(-1);
        var result = new List<IncomeByPosition>();
        foreach (var group in data.Dividends.GroupBy(static dividend => dividend.InstrumentID))
        {
            var payments = group.OrderBy(static dividend => dividend.PaidAt).ToList();
            var recent = payments.Where(dividend => data.LocalDay(dividend.PaidAt) > since).ToList();
            held.TryGetValue(group.Key, out var position);
            if (recent.Count == 0 && position is null)
            {
                continue;
            }

            var perShare = payments.Where(static dividend => dividend.PerShare is > 0).Select(static dividend => dividend.PerShare!.Value).ToList();
            decimal? last = perShare.Count > 0 ? perShare[^1] : null;
            decimal? previous = perShare.Count > 1 ? perShare[^2] : null;
            decimal? change = last is { } now && previous is > 0 ? now / previous.Value - 1m : null;
            var received = recent.Sum(static dividend => dividend.Amount);
            result.Add(new IncomeByPosition(
                group.Key,
                position?.Value ?? 0m,
                position?.Cost ?? 0m,
                received,
                recent.Sum(dividend => Withheld(dividend, data) ?? 0m),
                recent.Count,
                last,
                previous,
                change,
                change switch { > 0.01m => "raised", < -0.01m => "cut", not null => "steady", _ => string.Empty }));
        }

        return result.OrderByDescending(static item => item.Received).ThenByDescending(static item => item.Value).ToList();
    }

    /// <summary>
    /// The tax withheld on a payment: as imported, or worked out from the gross per share Trading 212's API gives (the
    /// gross at that day's rate minus what arrived).
    /// </summary>
    /// <param name="dividend">The payment.</param>
    /// <param name="data">The account.</param>
    /// <returns>The tax, or null when it can't be told.</returns>
    public static decimal? Withheld(DividendRow dividend, PortfolioData data)
    {
        if (dividend.Withheld is { } known)
        {
            return known;
        }

        if (dividend.Source != (byte)DataSource.Trading212 || dividend.PerShare is not > 0 || dividend.Quantity is not > 0
            || !data.Instruments.TryGetValue(dividend.InstrumentID, out var instrument)
            || string.Equals(instrument.Currency, data.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var gross = dividend.PerShare.Value * dividend.Quantity.Value * data.Prices.FxOn(instrument.Currency, data.LocalDay(dividend.PaidAt));
        var tax = gross - dividend.Amount;
        return tax > 0 && tax <= gross * 0.5m ? Math.Round(tax, 2) : null;
    }
}

/// <summary>The income of one instrument.</summary>
/// <param name="InstrumentId">The instrument.</param>
/// <param name="Value">What's held of it now.</param>
/// <param name="Cost">What it cost.</param>
/// <param name="Received">What it paid in the last 12 months.</param>
/// <param name="Withheld">The tax withheld at source in the last 12 months.</param>
/// <param name="Payments">The payments in the last 12 months.</param>
/// <param name="LastPerShare">The last payment per share.</param>
/// <param name="PreviousPerShare">The one before.</param>
/// <param name="Change">The change from one to the other.</param>
/// <param name="Trend">raised, cut, steady or empty.</param>
internal sealed record IncomeByPosition(
    int InstrumentId, decimal Value, decimal Cost, decimal Received, decimal Withheld, int Payments,
    decimal? LastPerShare, decimal? PreviousPerShare, decimal? Change, string Trend);
