namespace Obol.WebApi.Features.Portfolio.Services;

/// <summary>
/// Daily prices of the instruments (in their own currencies) and exchange rates into the account currency, with the
/// rule every chart uses: a day without a price takes the last one before it (or, before the first, the first one).
/// </summary>
internal sealed class PriceBook
{
    private readonly Dictionary<int, SortedList<DateOnly, decimal>> _prices = [];
    private readonly Dictionary<string, SortedList<DateOnly, decimal>> _fx = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _currency;

    /// <summary>
    /// Creates an empty book.
    /// </summary>
    /// <param name="accountCurrency">The account currency (rates are into it).</param>
    public PriceBook(string accountCurrency)
    {
        _currency = accountCurrency.ToUpperInvariant();
    }

    /// <summary>Gets the account currency.</summary>
    public string Currency => _currency;

    /// <summary>
    /// Adds a price. A price already known for the day is kept unless <paramref name="replace"/>.
    /// </summary>
    /// <param name="instrumentId">The instrument.</param>
    /// <param name="day">The day.</param>
    /// <param name="price">The price.</param>
    /// <param name="replace">Replace a price already known for that day.</param>
    public void AddPrice(int instrumentId, DateOnly day, decimal price, bool replace = true)
    {
        if (price <= 0)
        {
            return;
        }

        if (!_prices.TryGetValue(instrumentId, out var series))
        {
            series = [];
            _prices[instrumentId] = series;
        }

        if (replace || !series.ContainsKey(day))
        {
            series[day] = price;
        }
    }

    /// <summary>
    /// Adds an exchange rate (one <paramref name="currency"/> is <paramref name="rate"/> of the account currency).
    /// </summary>
    /// <param name="currency">The currency.</param>
    /// <param name="day">The day.</param>
    /// <param name="rate">The rate.</param>
    /// <param name="replace">Replace a rate already known for that day.</param>
    public void AddFx(string currency, DateOnly day, decimal rate, bool replace = true)
    {
        var key = currency.ToUpperInvariant();
        if (rate <= 0 || key == _currency)
        {
            return;
        }

        if (!_fx.TryGetValue(key, out var series))
        {
            series = [];
            _fx[key] = series;
        }

        if (replace || !series.ContainsKey(day))
        {
            series[day] = rate;
        }
    }

    /// <summary>
    /// Whether any price is known for an instrument.
    /// </summary>
    /// <param name="instrumentId">The instrument.</param>
    /// <returns>True when there is one.</returns>
    public bool HasPrices(int instrumentId) => _prices.TryGetValue(instrumentId, out var series) && series.Count > 0;

    /// <summary>
    /// The price on a day (the last one before it, or the first one after when there is none before).
    /// </summary>
    /// <param name="instrumentId">The instrument.</param>
    /// <param name="day">The day.</param>
    /// <returns>The price, or null when none is known.</returns>
    public decimal? PriceOn(int instrumentId, DateOnly day) => _prices.TryGetValue(instrumentId, out var series) ? On(series, day) : null;

    /// <summary>
    /// The days with a known price, oldest first.
    /// </summary>
    /// <param name="instrumentId">The instrument.</param>
    /// <returns>The days and prices.</returns>
    public IReadOnlyList<KeyValuePair<DateOnly, decimal>> Series(int instrumentId) =>
        _prices.TryGetValue(instrumentId, out var series) ? series.ToList() : [];

    /// <summary>
    /// The last two days with a price up to a day: the price "now" and the close before it.
    /// </summary>
    /// <param name="instrumentId">The instrument.</param>
    /// <param name="day">The day.</param>
    /// <returns>(last day, last price, previous day, previous price), or null.</returns>
    public (DateOnly Day, decimal Price, DateOnly PreviousDay, decimal Previous)? LastTwo(int instrumentId, DateOnly day)
    {
        if (!_prices.TryGetValue(instrumentId, out var series))
        {
            return null;
        }

        var index = LastIndexAtOrBefore(series.Keys, day);
        if (index < 1)
        {
            return null;
        }

        return (series.Keys[index], series.Values[index], series.Keys[index - 1], series.Values[index - 1]);
    }

    /// <summary>
    /// The exchange rate of a currency into the account currency on a day (1 for the account currency; pence are a
    /// hundredth of a pound).
    /// </summary>
    /// <param name="currency">The currency.</param>
    /// <param name="day">The day.</param>
    /// <returns>The rate (1 when nothing is known, so a value is never lost).</returns>
    public decimal FxOn(string currency, DateOnly day)
    {
        var key = (currency ?? string.Empty).ToUpperInvariant();
        if (key.Length == 0 || key == _currency)
        {
            return 1m;
        }

        if (_fx.TryGetValue(key, out var direct) && On(direct, day) is { } rate)
        {
            return rate;
        }

        // Pence (GBX, sometimes written GBp) from the pound, and the other way round.
        if (key == "GBX" && _fx.TryGetValue("GBP", out var pound) && On(pound, day) is { } gbp)
        {
            return gbp / 100m;
        }

        if (key == "GBX" && _currency == "GBP")
        {
            return 0.01m;
        }

        if (key == "GBP" && _fx.TryGetValue("GBX", out var pence) && On(pence, day) is { } gbx)
        {
            return gbx * 100m;
        }

        return 1m;
    }

    /// <summary>
    /// Whether a rate is known for a currency.
    /// </summary>
    /// <param name="currency">The currency.</param>
    /// <returns>True for the account currency or a currency with rates.</returns>
    public bool HasFx(string currency)
    {
        var key = (currency ?? string.Empty).ToUpperInvariant();
        return key == _currency || _fx.ContainsKey(key) || (key == "GBX" && (_fx.ContainsKey("GBP") || _currency == "GBP"));
    }

    private static decimal? On(SortedList<DateOnly, decimal> series, DateOnly day)
    {
        if (series.Count == 0)
        {
            return null;
        }

        var index = LastIndexAtOrBefore(series.Keys, day);
        return index >= 0 ? series.Values[index] : series.Values[0];
    }

    private static int LastIndexAtOrBefore(IList<DateOnly> keys, DateOnly day)
    {
        int low = 0, high = keys.Count - 1, found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (keys[middle] <= day)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }
}
