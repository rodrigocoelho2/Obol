namespace Obol.WebApi.Features;

/// <summary>
/// Module options, bound from the "Obol" configuration section (in Docker: Obol__ApiKey, Obol__EncryptionKey…).
/// </summary>
public sealed class ObolOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Obol";

    /// <summary>Gets or sets the key the Website must send in the x-api-key header. Never commit a real value.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the secret the Trading 212 API keys are encrypted with in the database (AES-GCM). Changing it makes the
    /// saved keys unreadable: everyone has to paste theirs again. Never commit a real value.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the time zone days are counted in (IANA name).</summary>
    public string TimeZone { get; set; } = "Europe/Lisbon";

    /// <summary>Gets or sets the currency of an account that has no Trading 212 connection (a CSV import only).</summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>Gets or sets how often (minutes) each Trading 212 connection is synced in the background; 0 turns it off.</summary>
    public int SyncMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets a value indicating whether daily closing prices (and exchange rates) are fetched from Yahoo Finance,
    /// so the charts go back before the first sync. Off: the charts are drawn from the trades and the syncs alone.
    /// </summary>
    public bool PriceHistory { get; set; } = true;

    /// <summary>
    /// Gets or sets the publishable key of logo.dev (pk_…), which turns the ticker badges into company logos. Logos are
    /// fetched by the WebApi, kept in the database and served by Obol, so the phone never asks a third party.
    /// Empty: ticker badges only.
    /// </summary>
    public string LogoToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether Obol runs on sample data kept in memory instead of SQL Server: a "demo"
    /// account with two years of a sample portfolio, and every new account gets the same. Nothing is saved.
    /// </summary>
    public bool DemoMode { get; set; }

    /// <summary>Gets or sets the password of the "demo" account in demo mode.</summary>
    public string DemoPassword { get; set; } = "obol-demo";

    /// <summary>Gets or sets the AI coach's provider: anthropic, gemini or openai (any OpenAI-compatible API, Ollama included). Empty = off.</summary>
    public string AiProvider { get; set; } = string.Empty;

    /// <summary>Gets or sets the provider's API key (not needed for a local Ollama). Never commit a real value.</summary>
    public string AiApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the model (empty = the provider's default).</summary>
    public string AiModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the address of an OpenAI-compatible API (Ollama: http://host:11434/v1).</summary>
    public string AiBaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the language the coach writes in.</summary>
    public string AiLanguage { get; set; } = "English";

    /// <summary>Gets or sets the Trading 212 address of real-money accounts.</summary>
    public string Trading212LiveUrl { get; set; } = "https://live.trading212.com/";

    /// <summary>Gets or sets the Trading 212 address of practice accounts.</summary>
    public string Trading212DemoUrl { get; set; } = "https://demo.trading212.com/";
}
