using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Portfolio.Models.Entities;

namespace Obol.WebApi.Features.Portfolio.Services;

/// <summary>Shares of one instrument inside a pie, or outside every pie, valued at today's prices.</summary>
/// <param name="Instrument">The instrument.</param>
/// <param name="Quantity">The shares.</param>
/// <param name="Value">What they are worth, in the account currency.</param>
/// <param name="Invested">What they cost.</param>
/// <param name="CurrentShare">The share of the pie now (null outside pies).</param>
/// <param name="ExpectedShare">The share of the pie as set (null outside pies).</param>
internal sealed record PieSlice(InstrumentRow Instrument, decimal Quantity, decimal Value, decimal Invested, decimal? CurrentShare = null, decimal? ExpectedShare = null)
{
    /// <summary>Gets the gain.</summary>
    public decimal Gain => Value - Invested;

    /// <summary>Gets the gain as a share of the cost.</summary>
    public decimal? GainPct => Invested > 0 ? Gain / Invested : null;
}

/// <summary>A pie and its instruments.</summary>
/// <param name="Pie">The pie as the last sync read it.</param>
/// <param name="Holdings">Its instruments, biggest first.</param>
internal sealed record PieGroup(PieRow Pie, IReadOnlyList<PieSlice> Holdings)
{
    /// <summary>Gets what the pie's shares are worth.</summary>
    public decimal Value => Holdings.Sum(static slice => slice.Value);

    /// <summary>Gets what was put into them.</summary>
    public decimal Invested => Holdings.Sum(static slice => slice.Invested);

    /// <summary>Gets the gain.</summary>
    public decimal Gain => Value - Invested;

    /// <summary>Gets the gain as a share of what was put in.</summary>
    public decimal? GainPct => Invested > 0 ? Gain / Invested : null;
}

/// <summary>
/// Splits the open positions into the Trading 212 pies and what is held outside them, the way Trading 212 shows the
/// portfolio. The shares a pie owns are valued with the position's value today (so the pies move with the prices like
/// every other screen); what they cost is what Trading 212 says the pie paid. What is left of a position outside the pies
/// keeps the rest of its cost.
/// </summary>
internal static class PieMath
{
    /// <summary>
    /// Groups the positions by pie.
    /// </summary>
    /// <param name="data">The account (its pies).</param>
    /// <param name="positions">The open positions.</param>
    /// <returns>The pies (biggest first, empty ones left out) and the shares outside them (biggest first).</returns>
    public static (List<PieGroup> Pies, List<PieSlice> Outside) Group(PortfolioData data, IReadOnlyList<Position> positions)
    {
        var byInstrument = positions.Where(static position => position.Quantity > 0)
            .GroupBy(static position => position.Instrument.InstrumentID)
            .ToDictionary(static group => group.Key, static group => group.First());
        var left = byInstrument.ToDictionary(static pair => pair.Key, static pair => pair.Value.Quantity);
        var paid = byInstrument.ToDictionary(static pair => pair.Key, static _ => 0m);

        var pies = new List<PieGroup>();
        foreach (var pie in data.Pies)
        {
            var slices = new List<PieSlice>();
            foreach (var holding in data.PieHoldings.Where(row => row.PieID == pie.PieID))
            {
                // A share the account no longer holds (sold since the pie was read) isn't counted anywhere else either.
                if (!byInstrument.TryGetValue(holding.InstrumentID, out var position) || left[holding.InstrumentID] <= 0)
                {
                    continue;
                }

                var quantity = Math.Min(holding.Quantity, left[holding.InstrumentID]);
                left[holding.InstrumentID] -= quantity;
                var value = position.Value * quantity / position.Quantity;
                var invested = holding.Quantity > 0 ? holding.Invested * quantity / holding.Quantity : 0m;
                paid[holding.InstrumentID] += invested;
                slices.Add(new PieSlice(position.Instrument, quantity, value, invested, holding.CurrentShare, holding.ExpectedShare));
            }

            if (slices.Count > 0)
            {
                pies.Add(new PieGroup(pie, slices.OrderByDescending(static slice => slice.Value).ToList()));
            }
        }

        var outside = new List<PieSlice>();
        foreach (var (id, position) in byInstrument)
        {
            var quantity = left[id];
            // Rounding of fractional shares leaves dust: anything under a millionth of the position is the pie's.
            if (quantity <= position.Quantity * 0.000001m)
            {
                continue;
            }

            var share = quantity / position.Quantity;
            var invested = paid[id] > 0 ? Math.Max(position.Cost - paid[id], 0m) : position.Cost;
            outside.Add(new PieSlice(position.Instrument, quantity, share >= 1 ? position.Value : position.Value * share, invested));
        }

        return (pies.OrderByDescending(static pie => pie.Value).ToList(), outside.OrderByDescending(static slice => slice.Value).ToList());
    }
}
