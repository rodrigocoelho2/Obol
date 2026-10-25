using Obol.WebApi.Extensions.Data;
using Obol.WebApi.Features.Instruments.Models.Entities;

namespace Obol.WebApi.Features.Instruments.Repositories;

/// <summary>
/// The instruments (dbo.Instrument), shared by every account.
/// </summary>
internal interface IInstrumentRepository
{
    /// <summary>Adds or updates instruments.</summary>
    /// <param name="rows">The instruments.</param>
    /// <returns>The instruments sent, with their ids.</returns>
    Task<List<InstrumentRow>> UpsertAsync(IReadOnlyCollection<InstrumentInput> rows);

    /// <summary>Lists every instrument.</summary>
    /// <returns>The instruments.</returns>
    Task<List<InstrumentRow>> ListAsync();

    /// <summary>Records the Yahoo Finance symbol of an instrument and how far back its prices go.</summary>
    /// <param name="instrumentId">The instrument.</param>
    /// <param name="priceSymbol">The symbol ('' = none).</param>
    /// <param name="pricesFrom">The first day fetched (null keeps it).</param>
    /// <returns>A task.</returns>
    Task SetPriceSymbolAsync(int instrumentId, string priceSymbol, DateOnly? pricesFrom);
}

/// <summary>Instruments in SQL Server.</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class InstrumentRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IInstrumentRepository
{
    /// <inheritdoc />
    public async Task<List<InstrumentRow>> UpsertAsync(IReadOnlyCollection<InstrumentInput> rows) =>
        rows.Count == 0 ? [] : await QueryAsync<InstrumentRow>("[dbo].[InstrumentUpsert]", new { Rows = Json(rows) });

    /// <inheritdoc />
    public Task<List<InstrumentRow>> ListAsync() => QueryAsync<InstrumentRow>("[dbo].[InstrumentList]");

    /// <inheritdoc />
    public Task SetPriceSymbolAsync(int instrumentId, string priceSymbol, DateOnly? pricesFrom) =>
        ExecuteAsync("[dbo].[InstrumentSetPriceSymbol]", new { InstrumentID = instrumentId, PriceSymbol = priceSymbol, PricesFrom = pricesFrom is { } day ? Date(day) : (DateTime?)null });
}

/// <summary>Instruments in demo mode (<see cref="MemoryStore"/>): same rules as the stored procedures.</summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemoryInstrumentRepository(MemoryStore store) : IInstrumentRepository
{
    /// <inheritdoc />
    public Task<List<InstrumentRow>> UpsertAsync(IReadOnlyCollection<InstrumentInput> rows)
    {
        lock (store.Gate)
        {
            var result = new List<InstrumentRow>();
            foreach (var input in rows.GroupBy(static row => row.Key).Select(static group => group.First()))
            {
                var row = store.Instruments.FirstOrDefault(instrument => instrument.InstrumentKey == input.Key);
                if (row is null)
                {
                    row = new InstrumentRow { InstrumentID = store.Instruments.Count + 1, InstrumentKey = input.Key, Ticker = input.Ticker, Symbol = input.Symbol, Name = input.Name };
                    store.Instruments.Add(row);
                }
                else if (input.Ticker.Contains('_') || !row.Ticker.Contains('_'))
                {
                    row.Ticker = input.Ticker;
                    row.Symbol = input.Symbol;
                    row.Name = input.Name;
                }

                row.Isin = input.Isin ?? row.Isin;
                row.Currency = input.Currency;
                row.AssetClass = input.AssetClass > 0 ? input.AssetClass : row.AssetClass;
                result.Add(Copy(row));
            }

            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<List<InstrumentRow>> ListAsync()
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Instruments.Select(Copy).ToList());
        }
    }

    /// <inheritdoc />
    public Task SetPriceSymbolAsync(int instrumentId, string priceSymbol, DateOnly? pricesFrom)
    {
        lock (store.Gate)
        {
            if (store.Instruments.FirstOrDefault(row => row.InstrumentID == instrumentId) is { } row)
            {
                row.PriceSymbol = priceSymbol;
                row.PricesFrom = pricesFrom is { } day ? day.ToDateTime(TimeOnly.MinValue) : row.PricesFrom;
            }
        }

        return Task.CompletedTask;
    }

    private static InstrumentRow Copy(InstrumentRow row) => new()
    {
        InstrumentID = row.InstrumentID,
        InstrumentKey = row.InstrumentKey,
        Isin = row.Isin,
        Ticker = row.Ticker,
        Symbol = row.Symbol,
        Name = row.Name,
        Currency = row.Currency,
        AssetClass = row.AssetClass,
        PriceSymbol = row.PriceSymbol,
        PricesFrom = row.PricesFrom,
    };
}
