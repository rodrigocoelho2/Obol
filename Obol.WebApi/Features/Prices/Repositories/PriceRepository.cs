using System.Text.Json.Serialization;
using Obol.WebApi.Extensions.Data;

namespace Obol.WebApi.Features.Prices.Repositories;

/// <summary>A closing price (dbo.PriceDay).</summary>
public sealed class PriceDayRow
{
    /// <summary>Gets or sets the instrument.</summary>
    public int InstrumentID { get; set; }

    /// <summary>Gets or sets the day.</summary>
    public DateTime Day { get; set; }

    /// <summary>Gets or sets the close, in the instrument currency.</summary>
    public decimal Close { get; set; }
}

/// <summary>An exchange rate (dbo.FxDay): one <see cref="Currency"/> is <see cref="Rate"/> of the account currency.</summary>
public sealed class FxDayRow
{
    /// <summary>Gets or sets the currency.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the day.</summary>
    public DateTime Day { get; set; }

    /// <summary>Gets or sets the rate.</summary>
    public decimal Rate { get; set; }
}

/// <summary>A closing price for dbo.PriceDaySave.</summary>
/// <param name="InstrumentID">The instrument.</param>
/// <param name="Day">The day (yyyy-MM-dd).</param>
/// <param name="Close">The close.</param>
/// <param name="Source">The source.</param>
public sealed record PriceInput(
    [property: JsonPropertyName("i")] int InstrumentID,
    [property: JsonPropertyName("d")] string Day,
    [property: JsonPropertyName("c")] decimal Close,
    [property: JsonPropertyName("s")] byte Source);

/// <summary>An exchange rate for dbo.FxDaySave.</summary>
/// <param name="Currency">The currency.</param>
/// <param name="Quote">The account currency.</param>
/// <param name="Day">The day (yyyy-MM-dd).</param>
/// <param name="Rate">The rate.</param>
public sealed record FxInput(
    [property: JsonPropertyName("c")] string Currency,
    [property: JsonPropertyName("q")] string Quote,
    [property: JsonPropertyName("d")] string Day,
    [property: JsonPropertyName("r")] decimal Rate);

/// <summary>
/// Closing prices and exchange rates, shared by every account.
/// </summary>
internal interface IPriceRepository
{
    /// <summary>Saves closing prices.</summary>
    /// <param name="rows">The prices.</param>
    /// <returns>A task.</returns>
    Task SavePricesAsync(IReadOnlyCollection<PriceInput> rows);

    /// <summary>Lists the prices of some instruments from a day on.</summary>
    /// <param name="instrumentIds">The instruments.</param>
    /// <param name="from">The first day.</param>
    /// <returns>The prices.</returns>
    Task<List<PriceDayRow>> ListPricesAsync(IReadOnlyCollection<int> instrumentIds, DateOnly from);

    /// <summary>Saves exchange rates.</summary>
    /// <param name="rows">The rates.</param>
    /// <returns>A task.</returns>
    Task SaveFxAsync(IReadOnlyCollection<FxInput> rows);

    /// <summary>Lists the rates into a currency from a day on.</summary>
    /// <param name="quote">The account currency.</param>
    /// <param name="from">The first day.</param>
    /// <returns>The rates.</returns>
    Task<List<FxDayRow>> ListFxAsync(string quote, DateOnly from);
}

/// <summary>Prices in SQL Server.</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class PriceRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IPriceRepository
{
    /// <inheritdoc />
    public async Task SavePricesAsync(IReadOnlyCollection<PriceInput> rows)
    {
        // Big backfills go in parts so one call stays a reasonable size.
        foreach (var chunk in rows.Chunk(5000))
        {
            await ExecuteAsync("[dbo].[PriceDaySave]", new { Rows = Json(chunk) });
        }
    }

    /// <inheritdoc />
    public async Task<List<PriceDayRow>> ListPricesAsync(IReadOnlyCollection<int> instrumentIds, DateOnly from) =>
        instrumentIds.Count == 0 ? [] : await QueryAsync<PriceDayRow>("[dbo].[PriceDayList]", new { InstrumentIDs = Json(instrumentIds), From = Date(from) });

    /// <inheritdoc />
    public async Task SaveFxAsync(IReadOnlyCollection<FxInput> rows)
    {
        foreach (var chunk in rows.Chunk(5000))
        {
            await ExecuteAsync("[dbo].[FxDaySave]", new { Rows = Json(chunk) });
        }
    }

    /// <inheritdoc />
    public Task<List<FxDayRow>> ListFxAsync(string quote, DateOnly from) => QueryAsync<FxDayRow>("[dbo].[FxDayList]", new { Quote = quote, From = Date(from) });
}

/// <summary>Prices in demo mode.</summary>
/// <param name="store">The in-memory tables.</param>
internal sealed class MemoryPriceRepository(MemoryStore store) : IPriceRepository
{
    /// <inheritdoc />
    public Task SavePricesAsync(IReadOnlyCollection<PriceInput> rows)
    {
        lock (store.Gate)
        {
            foreach (var row in rows.Where(static row => row.Close > 0))
            {
                MemoryStore.For(store.Prices, row.InstrumentID)[DbTime.ParseDay(row.Day)] = row.Close;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<PriceDayRow>> ListPricesAsync(IReadOnlyCollection<int> instrumentIds, DateOnly from)
    {
        lock (store.Gate)
        {
            var rows = new List<PriceDayRow>();
            foreach (var id in instrumentIds.Distinct())
            {
                if (store.Prices.TryGetValue(id, out var days))
                {
                    rows.AddRange(days.Where(day => day.Key >= from)
                        .Select(day => new PriceDayRow { InstrumentID = id, Day = day.Key.ToDateTime(TimeOnly.MinValue), Close = day.Value }));
                }
            }

            return Task.FromResult(rows);
        }
    }

    /// <inheritdoc />
    public Task SaveFxAsync(IReadOnlyCollection<FxInput> rows)
    {
        lock (store.Gate)
        {
            foreach (var row in rows.Where(static row => row.Rate > 0))
            {
                MemoryStore.For(store.Fx, (row.Currency, row.Quote))[DbTime.ParseDay(row.Day)] = row.Rate;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<FxDayRow>> ListFxAsync(string quote, DateOnly from)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Fx.Where(pair => pair.Key.Quote == quote)
                .SelectMany(pair => pair.Value.Where(day => day.Key >= from)
                    .Select(day => new FxDayRow { Currency = pair.Key.Currency, Day = day.Key.ToDateTime(TimeOnly.MinValue), Rate = day.Value }))
                .ToList());
        }
    }
}
