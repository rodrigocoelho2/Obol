using Obol.Library.Enums;

namespace Obol.WebApi.Features.Connections.Models.Entities;

/// <summary>The Trading 212 connection of an account (dbo.BrokerConnection).</summary>
public sealed class ConnectionRow
{
    /// <summary>Gets or sets the account.</summary>
    public Guid UserUniqueID { get; set; }

    /// <summary>Gets or sets "live" (real money) or "demo" (practice).</summary>
    public string Environment { get; set; } = "live";

    /// <summary>Gets or sets the encrypted API key.</summary>
    public string ApiKeyCipher { get; set; } = string.Empty;

    /// <summary>Gets or sets the encrypted API secret.</summary>
    public string ApiSecretCipher { get; set; } = string.Empty;

    /// <summary>Gets or sets the last characters of the key, to show which one it is.</summary>
    public string KeyHint { get; set; } = string.Empty;

    /// <summary>Gets or sets the Trading 212 account id.</summary>
    public long? AccountId { get; set; }

    /// <summary>Gets or sets the account currency.</summary>
    public string? Currency { get; set; }

    /// <summary>Gets or sets a value indicating whether positions and cash are read.</summary>
    public bool SyncPositions { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether orders are read.</summary>
    public bool SyncOrders { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether dividends are read.</summary>
    public bool SyncDividends { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether deposits, withdrawals and interest are read.</summary>
    public bool SyncTransactions { get; set; } = true;

    /// <summary>Gets or sets how the last sync went (<see cref="SyncState"/>).</summary>
    public byte State { get; set; }

    /// <summary>Gets or sets the last sync's message.</summary>
    public string LastMessage { get; set; } = string.Empty;

    /// <summary>Gets or sets when the last sync finished (UTC).</summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>Gets or sets when the whole history was first read (UTC).</summary>
    public DateTime? HistoryDoneAt { get; set; }

    /// <summary>Gets or sets when the connection was made (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>One sync or CSV import (dbo.SyncRun).</summary>
public sealed class SyncRunRow
{
    /// <summary>Gets or sets the id.</summary>
    public long SyncRunID { get; set; }

    /// <summary>Gets or sets the source (<see cref="DataSource"/>).</summary>
    public byte Source { get; set; }

    /// <summary>Gets or sets when it started (UTC).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Gets or sets when it finished (UTC).</summary>
    public DateTime FinishedAt { get; set; }

    /// <summary>Gets or sets a value indicating whether it worked.</summary>
    public bool Ok { get; set; }

    /// <summary>Gets or sets the one-line summary.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the steps, as a JSON array of strings.</summary>
    public string Lines { get; set; } = "[]";
}
