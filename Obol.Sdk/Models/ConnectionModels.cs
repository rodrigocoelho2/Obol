using Obol.Library.Enums;

namespace Obol.Sdk.Models;

/// <summary>One sync or CSV import.</summary>
public sealed class SyncRunDTO
{
    /// <summary>Gets or sets the id.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets where it read from.</summary>
    public DataSource Source { get; set; }

    /// <summary>Gets or sets when it started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets when it finished.</summary>
    public DateTimeOffset FinishedAt { get; set; }

    /// <summary>Gets or sets a value indicating whether it worked.</summary>
    public bool Ok { get; set; }

    /// <summary>Gets or sets the one-line summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the steps.</summary>
    public List<string> Lines { get; set; } = [];
}

/// <summary>The Sync screen: the Trading 212 connection, what it syncs and the last runs.</summary>
public sealed class ConnectionDTO
{
    /// <summary>Gets or sets a value indicating whether a key is saved.</summary>
    public bool Connected { get; set; }

    /// <summary>Gets or sets "live" or "demo".</summary>
    public string? Environment { get; set; }

    /// <summary>Gets or sets the last characters of the key.</summary>
    public string? KeyHint { get; set; }

    /// <summary>Gets or sets the Trading 212 account id.</summary>
    public long? AccountId { get; set; }

    /// <summary>Gets or sets the account currency.</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets a value indicating whether positions and cash are synced.</summary>
    public bool SyncPositions { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether orders are synced.</summary>
    public bool SyncOrders { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether dividends are synced.</summary>
    public bool SyncDividends { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether deposits, withdrawals and interest are synced.</summary>
    public bool SyncTransactions { get; set; } = true;

    /// <summary>Gets or sets how the last sync went (Running while one is waiting or running).</summary>
    public SyncState State { get; set; }

    /// <summary>Gets or sets the last sync's message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets when the last sync finished.</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Gets or sets when the next background sync is due.</summary>
    public DateTimeOffset? NextSyncAt { get; set; }

    /// <summary>Gets or sets a value indicating whether the whole history has been read.</summary>
    public bool HistoryDone { get; set; }

    /// <summary>Gets or sets how often the background sync runs (minutes; 0 = off).</summary>
    public int SyncMinutes { get; set; }

    /// <summary>Gets or sets a value indicating whether daily prices come from Yahoo Finance.</summary>
    public bool PriceHistory { get; set; }

    /// <summary>Gets or sets a value indicating whether logos are on.</summary>
    public bool Logos { get; set; }

    /// <summary>Gets or sets a value indicating whether the account has any portfolio data.</summary>
    public bool HasData { get; set; }

    /// <summary>Gets or sets how many trades, dividends and transfers are kept.</summary>
    public int Records { get; set; }

    /// <summary>Gets or sets the last runs, newest first.</summary>
    public List<SyncRunDTO> Runs { get; set; } = [];
}

/// <summary>Connects (or reconnects) Trading 212.</summary>
public sealed class ConnectRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the API secret.</summary>
    public string? ApiSecret { get; set; }

    /// <summary>Gets or sets "live" (real money) or "demo" (practice).</summary>
    public string? Environment { get; set; }
}

/// <summary>What the sync reads.</summary>
public sealed class ConnectionOptionsRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets a value indicating whether positions and cash are synced.</summary>
    public bool SyncPositions { get; set; }

    /// <summary>Gets or sets a value indicating whether orders are synced.</summary>
    public bool SyncOrders { get; set; }

    /// <summary>Gets or sets a value indicating whether dividends are synced.</summary>
    public bool SyncDividends { get; set; }

    /// <summary>Gets or sets a value indicating whether deposits, withdrawals and interest are synced.</summary>
    public bool SyncTransactions { get; set; }
}

/// <summary>What a CSV import added.</summary>
public sealed class CsvImportResultDTO
{
    /// <summary>Gets or sets the new trades.</summary>
    public int Trades { get; set; }

    /// <summary>Gets or sets the new dividends.</summary>
    public int Dividends { get; set; }

    /// <summary>Gets or sets the new deposits, withdrawals, interest and fees.</summary>
    public int Cash { get; set; }

    /// <summary>Gets or sets the instruments in the file.</summary>
    public int Instruments { get; set; }

    /// <summary>Gets or sets the lines already kept (imported before).</summary>
    public int Duplicates { get; set; }

    /// <summary>Gets or sets the lines not kept.</summary>
    public int Skipped { get; set; }

    /// <summary>Gets or sets the summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the details.</summary>
    public List<string> Lines { get; set; } = [];
}
