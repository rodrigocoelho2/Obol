using Obol.WebApi.Extensions.Data;

namespace Obol.WebApi.Features.Logos.Repositories;

/// <summary>A logo kept for an instrument (dbo.InstrumentLogo).</summary>
public sealed class LogoRow
{
    /// <summary>Gets or sets the instrument key.</summary>
    public string InstrumentKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the content type (image/png…).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets the image (null: the logo service has none).</summary>
    public byte[]? Data { get; set; }

    /// <summary>Gets or sets when it was fetched (UTC).</summary>
    public DateTime FetchedAt { get; set; }
}

/// <summary>
/// The logos fetched from the logo service.
/// </summary>
internal interface ILogoRepository
{
    /// <summary>Gets the logo kept for an instrument.</summary>
    /// <param name="instrumentKey">The instrument key.</param>
    /// <returns>The logo, or null when it was never fetched.</returns>
    Task<LogoRow?> GetAsync(string instrumentKey);

    /// <summary>Keeps a logo (or that there is none).</summary>
    /// <param name="instrumentKey">The instrument key.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="data">The image, or null.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(string instrumentKey, string contentType, byte[]? data);
}

/// <summary>Logos in SQL Server.</summary>
/// <param name="connectionFactory">Opens connections.</param>
internal sealed class LogoRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), ILogoRepository
{
    /// <inheritdoc />
    public Task<LogoRow?> GetAsync(string instrumentKey) => QuerySingleOrDefaultAsync<LogoRow>("[dbo].[InstrumentLogoGet]", new { InstrumentKey = instrumentKey });

    /// <inheritdoc />
    public Task SaveAsync(string instrumentKey, string contentType, byte[]? data) =>
        ExecuteAsync("[dbo].[InstrumentLogoSave]", new { InstrumentKey = instrumentKey, ContentType = contentType, Data = data });
}

/// <summary>Logos in demo mode.</summary>
/// <param name="store">The in-memory tables.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed class MemoryLogoRepository(MemoryStore store, TimeProvider timeProvider) : ILogoRepository
{
    /// <inheritdoc />
    public Task<LogoRow?> GetAsync(string instrumentKey)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Logos.TryGetValue(instrumentKey, out var row) ? row : null);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(string instrumentKey, string contentType, byte[]? data)
    {
        lock (store.Gate)
        {
            store.Logos[instrumentKey] = new LogoRow { InstrumentKey = instrumentKey, ContentType = contentType, Data = data, FetchedAt = timeProvider.GetUtcNow().UtcDateTime };
        }

        return Task.CompletedTask;
    }
}
