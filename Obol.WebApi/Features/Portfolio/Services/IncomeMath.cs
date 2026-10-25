using Obol.Library.Enums;

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
}
