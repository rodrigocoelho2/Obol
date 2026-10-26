namespace Obol.WebApi.Features.Trading212;

/// <summary>The key, secret and kind of account a call is made with.</summary>
/// <param name="Environment">"live" (real money) or "demo" (practice).</param>
/// <param name="ApiKey">The API key.</param>
/// <param name="ApiSecret">The API secret.</param>
internal sealed record Trading212Credentials(string Environment, string ApiKey, string ApiSecret);

/// <summary>GET /api/v0/equity/account/summary.</summary>
internal sealed class T212AccountSummary
{
    public long Id { get; set; }

    public string? Currency { get; set; }

    public decimal TotalValue { get; set; }

    public T212Cash? Cash { get; set; }

    public T212Investments? Investments { get; set; }
}

/// <summary>The cash part of the account summary.</summary>
internal sealed class T212Cash
{
    public decimal AvailableToTrade { get; set; }

    public decimal InPies { get; set; }

    public decimal ReservedForOrders { get; set; }
}

/// <summary>The investments part of the account summary.</summary>
internal sealed class T212Investments
{
    public decimal CurrentValue { get; set; }

    public decimal RealizedProfitLoss { get; set; }

    public decimal TotalCost { get; set; }

    public decimal UnrealizedProfitLoss { get; set; }
}

/// <summary>An instrument inside a position, an order or a dividend.</summary>
internal sealed class T212Instrument
{
    public string? Currency { get; set; }

    public string? Isin { get; set; }

    public string? Name { get; set; }

    public string? Ticker { get; set; }
}

/// <summary>GET /api/v0/equity/positions.</summary>
internal sealed class T212Position
{
    public decimal AveragePricePaid { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public decimal CurrentPrice { get; set; }

    public T212Instrument? Instrument { get; set; }

    public decimal Quantity { get; set; }

    public T212PositionWallet? WalletImpact { get; set; }
}

/// <summary>A position in the account currency.</summary>
internal sealed class T212PositionWallet
{
    public string? Currency { get; set; }

    public decimal CurrentValue { get; set; }

    public decimal? FxImpact { get; set; }

    public decimal TotalCost { get; set; }

    public decimal UnrealizedProfitLoss { get; set; }
}

/// <summary>GET /api/v0/equity/metadata/instruments.</summary>
internal sealed class T212TradableInstrument
{
    public string? Ticker { get; set; }

    public string? Isin { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? CurrencyCode { get; set; }

    public string? Type { get; set; }
}

/// <summary>A page of a history endpoint.</summary>
/// <typeparam name="T">The item.</typeparam>
internal sealed class T212Page<T>
{
    public List<T> Items { get; set; } = [];

    public string? NextPagePath { get; set; }
}

/// <summary>GET /api/v0/equity/history/orders.</summary>
internal sealed class T212HistoricalOrder
{
    public T212Fill? Fill { get; set; }

    public T212Order? Order { get; set; }
}

/// <summary>How an order was filled.</summary>
internal sealed class T212Fill
{
    public DateTimeOffset? FilledAt { get; set; }

    public long Id { get; set; }

    public decimal Price { get; set; }

    public decimal Quantity { get; set; }

    public string? Type { get; set; }

    public T212FillWallet? WalletImpact { get; set; }
}

/// <summary>A fill in the account currency.</summary>
internal sealed class T212FillWallet
{
    public string? Currency { get; set; }

    public decimal? FxRate { get; set; }

    public decimal NetValue { get; set; }

    public decimal? RealisedProfitLoss { get; set; }

    public List<T212Tax>? Taxes { get; set; }
}

/// <summary>A fee or tax on a fill.</summary>
internal sealed class T212Tax
{
    public string? Currency { get; set; }

    public string? Name { get; set; }

    public decimal Quantity { get; set; }
}

/// <summary>The order of a fill.</summary>
internal sealed class T212Order
{
    public long Id { get; set; }

    public string? Currency { get; set; }

    public decimal? FilledQuantity { get; set; }

    public decimal? FilledValue { get; set; }

    public T212Instrument? Instrument { get; set; }

    public string? Side { get; set; }

    public string? Status { get; set; }

    public string? Ticker { get; set; }

    public string? Type { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>GET /api/v0/equity/history/dividends.</summary>
internal sealed class T212Dividend
{
    public decimal Amount { get; set; }

    public decimal? AmountInEuro { get; set; }

    public string? Currency { get; set; }

    public decimal? GrossAmountPerShare { get; set; }

    public T212Instrument? Instrument { get; set; }

    public DateTimeOffset PaidOn { get; set; }

    public decimal? Quantity { get; set; }

    public string? Reference { get; set; }

    public string? Ticker { get; set; }

    public string? TickerCurrency { get; set; }

    public string? Type { get; set; }
}

/// <summary>GET /api/v0/equity/history/transactions.</summary>
internal sealed class T212Transaction
{
    public decimal Amount { get; set; }

    public string? Currency { get; set; }

    public DateTimeOffset DateTime { get; set; }

    public string? Reference { get; set; }

    public string? Type { get; set; }
}

/// <summary>
/// Trading 212 answered with an error, or could not be reached.
/// </summary>
/// <param name="kind">What went wrong.</param>
/// <param name="message">A short description (never the key).</param>
internal sealed class Trading212Exception(Trading212Failure kind, string message) : Exception(message)
{
    /// <summary>Gets what went wrong.</summary>
    public Trading212Failure Kind { get; } = kind;
}

/// <summary>What went wrong with a Trading 212 call.</summary>
internal enum Trading212Failure
{
    /// <summary>The key or secret was refused (401).</summary>
    Unauthorized,

    /// <summary>The key is valid but lacks the permission for that endpoint (403).</summary>
    Forbidden,

    /// <summary>Too many calls, even after waiting.</summary>
    RateLimited,

    /// <summary>A network problem, a timeout or a 5xx.</summary>
    Unreachable,

    /// <summary>An answer Obol couldn't read.</summary>
    Unexpected,
}
