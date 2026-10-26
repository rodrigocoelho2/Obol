using Obol.WebApi.Features.Connections.Models.Entities;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Logos.Repositories;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Users.Models.Entities;

namespace Obol.WebApi.Extensions.Data;

/// <summary>
/// The tables of demo mode (<see cref="Features.ObolOptions.DemoMode"/>): the same shapes as the SQL Server tables,
/// kept in memory and lost on restart. The Memory* repositories read and write it under <see cref="Gate"/>.
/// </summary>
internal sealed class MemoryStore
{
    /// <summary>Gets the lock every memory repository takes.</summary>
    public Lock Gate { get; } = new();

    /// <summary>Gets the accounts.</summary>
    public List<UserAccount> Users { get; } = [];

    /// <summary>Gets the Trading 212 connections, by account.</summary>
    public Dictionary<Guid, ConnectionRow> Connections { get; } = [];

    /// <summary>Gets the sync log, by account (newest first).</summary>
    public Dictionary<Guid, List<SyncRunRow>> Runs { get; } = [];

    /// <summary>Gets the instruments.</summary>
    public List<InstrumentRow> Instruments { get; } = [];

    /// <summary>Gets the portfolios, by account.</summary>
    public Dictionary<Guid, MemoryPortfolio> Portfolios { get; } = [];

    /// <summary>Gets the closing prices, by instrument then day.</summary>
    public Dictionary<int, SortedDictionary<DateOnly, decimal>> Prices { get; } = [];

    /// <summary>Gets the exchange rates, by (currency, account currency) then day.</summary>
    public Dictionary<(string Currency, string Quote), SortedDictionary<DateOnly, decimal>> Fx { get; } = [];

    /// <summary>Gets the logos, by instrument key.</summary>
    public Dictionary<string, LogoRow> Logos { get; } = [];

    /// <summary>
    /// Gets (creating it) the per-key part of a table.
    /// </summary>
    /// <typeparam name="TKey">The key.</typeparam>
    /// <typeparam name="T">The per-key collection.</typeparam>
    /// <param name="table">The table.</param>
    /// <param name="key">The key.</param>
    /// <returns>The rows.</returns>
    public static T For<TKey, T>(Dictionary<TKey, T> table, TKey key)
        where TKey : notnull
        where T : new()
    {
        if (!table.TryGetValue(key, out var rows))
        {
            rows = new T();
            table[key] = rows;
        }

        return rows;
    }
}

/// <summary>The portfolio tables of one account in demo mode.</summary>
internal sealed class MemoryPortfolio
{
    /// <summary>Gets the open positions.</summary>
    public List<HoldingRow> Holdings { get; } = [];

    /// <summary>Gets the Trading 212 pies.</summary>
    public List<PieRow> Pies { get; } = [];

    /// <summary>Gets the instruments of the pies.</summary>
    public List<PieHoldingRow> PieHoldings { get; } = [];

    /// <summary>Gets the trades, oldest first.</summary>
    public List<TradeRow> Trades { get; } = [];

    /// <summary>Gets the dividends, oldest first.</summary>
    public List<DividendRow> Dividends { get; } = [];

    /// <summary>Gets the cash movements, oldest first.</summary>
    public List<CashMovementRow> Cash { get; } = [];

    /// <summary>Gets the daily snapshots.</summary>
    public SortedDictionary<DateOnly, SnapshotRow> Snapshots { get; } = [];
}
