using System.Globalization;
using Obol.Library.Enums;
using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Repositories;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Portfolio.Repositories;

namespace Obol.WebApi.Features.Portfolio.Services;

/// <summary>A trade as an import reads it.</summary>
/// <param name="InstrumentKey">The instrument (ISIN or T212:ticker).</param>
/// <param name="Buy">True for a buy.</param>
/// <param name="Quantity">Shares (positive).</param>
/// <param name="Price">Price per share.</param>
/// <param name="PriceCurrency">Currency of the price.</param>
/// <param name="FxRate">The broker's exchange rate, when there was one.</param>
/// <param name="Total">Paid or received, in the account currency (positive).</param>
/// <param name="RealizedPL">Realised gain of a sell.</param>
/// <param name="Fees">Fees and taxes, in the account currency.</param>
/// <param name="ExecutedAt">When it was filled (UTC).</param>
/// <param name="Source">Where it came from.</param>
/// <param name="ExternalId">The id at the broker.</param>
internal sealed record ImportedTrade(
    string InstrumentKey, bool Buy, decimal Quantity, decimal Price, string PriceCurrency, decimal? FxRate, decimal Total,
    decimal? RealizedPL, decimal Fees, DateTime ExecutedAt, DataSource Source, string? ExternalId);

/// <summary>A dividend as an import reads it.</summary>
/// <param name="InstrumentKey">The instrument.</param>
/// <param name="PaidAt">When it was paid (UTC).</param>
/// <param name="Quantity">Shares it was paid on.</param>
/// <param name="PerShare">Gross per share, in the instrument currency.</param>
/// <param name="Amount">Net amount, in the account currency.</param>
/// <param name="Withheld">Tax withheld, in the account currency.</param>
/// <param name="Kind">Kind (ORDINARY…).</param>
/// <param name="Source">Where it came from.</param>
internal sealed record ImportedDividend(
    string InstrumentKey, DateTime PaidAt, decimal? Quantity, decimal? PerShare, decimal Amount, decimal? Withheld, string Kind, DataSource Source);

/// <summary>A deposit, withdrawal, interest payment or fee as an import reads it.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Amount">Signed amount, in the account currency.</param>
/// <param name="At">When (UTC).</param>
/// <param name="Note">A note.</param>
/// <param name="Source">Where it came from.</param>
internal sealed record ImportedCash(CashKind Kind, decimal Amount, DateTime At, string Note, DataSource Source);

/// <summary>
/// What an import (the API, a CSV export, the demo data) read, before it is saved.
/// </summary>
internal sealed class ImportBatch
{
    /// <summary>Gets the instruments mentioned.</summary>
    public Dictionary<string, InstrumentInput> Instruments { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the trades.</summary>
    public List<ImportedTrade> Trades { get; } = [];

    /// <summary>Gets the dividends.</summary>
    public List<ImportedDividend> Dividends { get; } = [];

    /// <summary>Gets the cash movements.</summary>
    public List<ImportedCash> Cash { get; } = [];

    /// <summary>Gets or sets the account currency, when the import says.</summary>
    public string? Currency { get; set; }

    /// <summary>Gets or sets how many lines were not understood or not kept.</summary>
    public int Skipped { get; set; }

    /// <summary>Gets the notes about skipped lines ("3 currency conversions").</summary>
    public Dictionary<string, int> SkippedKinds { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets a value indicating whether the batch has anything to save.</summary>
    public bool IsEmpty => Trades.Count == 0 && Dividends.Count == 0 && Cash.Count == 0;

    /// <summary>
    /// Adds an instrument (the first description of a key wins, except that a known asset class fills an unknown one).
    /// </summary>
    /// <param name="instrument">The instrument.</param>
    public void Add(InstrumentInput instrument)
    {
        if (!Instruments.TryGetValue(instrument.Key, out var known))
        {
            Instruments[instrument.Key] = instrument;
        }
        else if (known.AssetClass == 0 && instrument.AssetClass > 0)
        {
            Instruments[instrument.Key] = known with { AssetClass = instrument.AssetClass };
        }
    }

    /// <summary>
    /// Counts a line that was not kept.
    /// </summary>
    /// <param name="kind">What it was.</param>
    public void Skip(string kind)
    {
        Skipped++;
        SkippedKinds[kind] = SkippedKinds.GetValueOrDefault(kind) + 1;
    }
}

/// <summary>How many records an import added.</summary>
/// <param name="Trades">New trades.</param>
/// <param name="Dividends">New dividends.</param>
/// <param name="Cash">New cash movements.</param>
/// <param name="Instruments">Instruments mentioned.</param>
/// <param name="Ids">The id of each instrument key.</param>
internal sealed record ImportCounts(int Trades, int Dividends, int Cash, int Instruments, IReadOnlyDictionary<string, int> Ids);

/// <summary>
/// Saves an <see cref="ImportBatch"/>: the instruments first (for their ids), then trades, dividends and cash. The keys
/// make the same record from the API and from a CSV export one row, so importing twice adds nothing.
/// </summary>
/// <param name="instrumentRepository">Instruments.</param>
/// <param name="portfolioRepository">The portfolio.</param>
internal sealed class PortfolioWriter(IInstrumentRepository instrumentRepository, IPortfolioRepository portfolioRepository)
{
    /// <summary>
    /// Saves a batch.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="batch">What was read.</param>
    /// <returns>How many records were new.</returns>
    public async Task<ImportCounts> SaveAsync(Guid userId, ImportBatch batch)
    {
        var instruments = await instrumentRepository.UpsertAsync(batch.Instruments.Values.ToList());
        var ids = instruments.ToDictionary(static row => row.InstrumentKey, static row => row.InstrumentID, StringComparer.Ordinal);

        var trades = batch.Trades.Where(trade => ids.ContainsKey(trade.InstrumentKey)).Select(trade => new TradeInput(
            ids[trade.InstrumentKey], TradeKey(trade), (byte)(trade.Buy ? 1 : 2), trade.Quantity, trade.Price, trade.PriceCurrency,
            trade.FxRate, Math.Abs(trade.Total), trade.RealizedPL, Math.Abs(trade.Fees), DbTime.Time(trade.ExecutedAt), (byte)trade.Source,
            trade.ExternalId)).ToList();
        var dividends = batch.Dividends.Where(dividend => ids.ContainsKey(dividend.InstrumentKey)).Select(dividend => new DividendInput(
            ids[dividend.InstrumentKey], DividendKey(dividend), DbTime.Time(dividend.PaidAt), dividend.Quantity, dividend.PerShare,
            dividend.Amount, dividend.Withheld, InstrumentNames.Truncate(dividend.Kind, 60), (byte)dividend.Source)).ToList();
        var cash = batch.Cash.Select(movement => new CashInput(
            CashKey(movement), (byte)movement.Kind, movement.Amount, DbTime.Time(movement.At), InstrumentNames.Truncate(movement.Note, 100),
            (byte)movement.Source)).ToList();

        var newTrades = await portfolioRepository.SaveTradesAsync(userId, trades);
        var newDividends = await portfolioRepository.SaveDividendsAsync(userId, dividends);
        var newCash = await portfolioRepository.SaveCashAsync(userId, cash);
        return new ImportCounts(newTrades, newDividends, newCash, ids.Count, ids);
    }

    /// <summary>The key of a trade: instrument, second, side and quantity.</summary>
    /// <param name="trade">The trade.</param>
    /// <returns>The key.</returns>
    internal static string TradeKey(ImportedTrade trade) => string.Create(
        CultureInfo.InvariantCulture, $"{trade.InstrumentKey}|{trade.ExecutedAt:yyyyMMddHHmmss}|{(trade.Buy ? 'B' : 'S')}|{trade.Quantity:0.######}");

    /// <summary>The key of a dividend: instrument, day and amount.</summary>
    /// <param name="dividend">The dividend.</param>
    /// <returns>The key.</returns>
    internal static string DividendKey(ImportedDividend dividend) => string.Create(
        CultureInfo.InvariantCulture, $"{dividend.InstrumentKey}|{dividend.PaidAt:yyyyMMdd}|{dividend.Amount:0.00}");

    /// <summary>The key of a cash movement: kind, second and amount.</summary>
    /// <param name="cash">The movement.</param>
    /// <returns>The key.</returns>
    internal static string CashKey(ImportedCash cash) => string.Create(
        CultureInfo.InvariantCulture, $"{(byte)cash.Kind}|{cash.At:yyyyMMddHHmmss}|{cash.Amount:0.00}");
}
