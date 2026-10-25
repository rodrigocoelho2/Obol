namespace Obol.Library.Enums;

/// <summary>
/// What an account may do.
/// </summary>
public enum UserRole
{
    /// <summary>Sees and syncs only its own portfolio.</summary>
    User,

    /// <summary>The first account: can also add and remove accounts.</summary>
    Admin,
}

/// <summary>
/// The kind of an instrument, which decides its slice of the allocation.
/// </summary>
public enum AssetClass : byte
{
    /// <summary>Anything else (a warrant, a right…).</summary>
    Other = 0,

    /// <summary>A share in one company.</summary>
    Stock = 1,

    /// <summary>An ETF that holds shares.</summary>
    EquityEtf = 2,

    /// <summary>An ETF that holds bonds.</summary>
    BondEtf = 3,

    /// <summary>A cryptocurrency.</summary>
    Crypto = 4,

    /// <summary>An ETF or ETC on gold, oil or other commodities.</summary>
    Commodity = 5,
}

/// <summary>
/// One line of the activity: a trade, an income or a cash movement.
/// </summary>
public enum ActivityKind
{
    /// <summary>Shares bought.</summary>
    Buy,

    /// <summary>Shares sold.</summary>
    Sell,

    /// <summary>A dividend paid.</summary>
    Dividend,

    /// <summary>Interest on uninvested cash (or share lending).</summary>
    Interest,

    /// <summary>Money put into the account.</summary>
    Deposit,

    /// <summary>Money taken out of the account.</summary>
    Withdrawal,

    /// <summary>A fee charged by the broker.</summary>
    Fee,
}

/// <summary>
/// The kind of a cash movement that is not a trade or a dividend.
/// </summary>
public enum CashKind : byte
{
    /// <summary>Money put into the account.</summary>
    Deposit = 1,

    /// <summary>Money taken out of the account.</summary>
    Withdrawal = 2,

    /// <summary>Interest on uninvested cash or share lending.</summary>
    Interest = 3,

    /// <summary>A fee charged by the broker.</summary>
    Fee = 4,

    /// <summary>Anything else that moves cash (a transfer, a card payment…).</summary>
    Other = 5,
}

/// <summary>
/// Where a record came from.
/// </summary>
public enum DataSource : byte
{
    /// <summary>The Trading 212 API.</summary>
    Trading212 = 1,

    /// <summary>A Trading 212 CSV export.</summary>
    Csv = 2,

    /// <summary>The sample data of demo mode.</summary>
    Demo = 3,

    /// <summary>Daily closing prices from Yahoo Finance.</summary>
    Yahoo = 4,
}

/// <summary>
/// The period a chart covers.
/// </summary>
public enum ChartRange
{
    /// <summary>The last seven days.</summary>
    Week,

    /// <summary>The last month.</summary>
    Month,

    /// <summary>The last three months.</summary>
    Quarter,

    /// <summary>Since 1 January.</summary>
    YearToDate,

    /// <summary>The last twelve months.</summary>
    Year,

    /// <summary>Since the first deposit.</summary>
    All,
}

/// <summary>
/// How the last sync of a connection went.
/// </summary>
public enum SyncState : byte
{
    /// <summary>Not synced yet.</summary>
    Never = 0,

    /// <summary>The last sync worked.</summary>
    Ok = 1,

    /// <summary>The last sync failed (the message says why).</summary>
    Failed = 2,

    /// <summary>A sync is running right now.</summary>
    Running = 3,
}
