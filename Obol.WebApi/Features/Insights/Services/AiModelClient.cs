using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Obol.WebApi.Features.Insights.Services;

/// <summary>Sends one prompt to the AI provider set up in the options (faked in tests).</summary>
internal interface IAiModelClient
{
    /// <summary>Gets a value indicating whether a provider is set up.</summary>
    bool Enabled { get; }

    /// <summary>Gets the provider name.</summary>
    string Provider { get; }

    /// <summary>Gets the model used.</summary>
    string Model { get; }

    /// <summary>Asks the model.</summary>
    /// <param name="system">The instructions.</param>
    /// <param name="user">The data.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The model's answer.</returns>
    Task<string> CompleteAsync(string system, string user, CancellationToken cancellationToken);
}

/// <summary>
/// Claude through the Anthropic Messages API, Gemini through its OpenAI-compatible endpoint, or any OpenAI-compatible
/// chat API (OpenAI, Ollama, LM Studio, vLLM).
/// </summary>
/// <param name="options">The options (provider, key, model, address).</param>
internal sealed class AiModelClient(IOptions<ObolOptions> options) : IAiModelClient
{
    /// <summary>The default Claude model.</summary>
    public const string DefaultAnthropicModel = "claude-haiku-5-5";

    /// <summary>The default model of an OpenAI-compatible API.</summary>
    public const string DefaultOpenAiModel = "gpt-4o-mini";

    /// <summary>The default Gemini model.</summary>
    public const string DefaultGeminiModel = "gemini-3.8-flash";

    /// <summary>Gemini's OpenAI-compatible endpoint (https://ai.google.dev/gemini-api/docs/openai).</summary>
    public const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai";

    // Local models on a NAS can take a few minutes for a report.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private ObolOptions Options => options.Value;

    /// <inheritdoc />
    public string Provider => (Options.AiProvider ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "anthropic" or "claude" => "anthropic",
        "gemini" or "google" => "gemini",
        "openai" or "ollama" or "openai-compatible" => "openai",
        _ => string.Empty,
    };

    /// <inheritdoc />
    public bool Enabled => Provider switch
    {
        "anthropic" or "gemini" => !string.IsNullOrWhiteSpace(Options.AiApiKey),
        "openai" => !string.IsNullOrWhiteSpace(Options.AiBaseUrl) || !string.IsNullOrWhiteSpace(Options.AiApiKey),
        _ => false,
    };

    /// <inheritdoc />
    public string Model => !string.IsNullOrWhiteSpace(Options.AiModel) ? Options.AiModel.Trim()
        : Provider switch { "anthropic" => DefaultAnthropicModel, "gemini" => DefaultGeminiModel, _ => DefaultOpenAiModel };

    /// <inheritdoc />
    public Task<string> CompleteAsync(string system, string user, CancellationToken cancellationToken) =>
        Provider == "anthropic" ? AnthropicAsync(system, user, cancellationToken) : OpenAiAsync(system, user, cancellationToken);

    private async Task<string> AnthropicAsync(string system, string user, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = Json(new
            {
                model = Model,
                max_tokens = 1800,
                system,
                messages = new[] { new { role = "user", content = user } },
            }),
        };
        request.Headers.TryAddWithoutValidation("x-api-key", Options.AiApiKey.Trim());
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        using var response = await Http.SendAsync(request, cancellationToken);
        using var document = await ReadAsync(response, cancellationToken);
        return string.Concat(document.RootElement.GetProperty("content").EnumerateArray()
            .Where(static block => block.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(static block => block.GetProperty("text").GetString()));
    }

    private async Task<string> OpenAiAsync(string system, string user, CancellationToken cancellationToken)
    {
        // Gemini speaks the same chat API at its own address.
        var baseUrl = !string.IsNullOrWhiteSpace(Options.AiBaseUrl) ? Options.AiBaseUrl.Trim().TrimEnd('/')
            : Provider == "gemini" ? GeminiBaseUrl
            : "https://api.openai.com/v1";
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/chat/completions")
        {
            Content = Json(new
            {
                model = Model,
                temperature = 0.4,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
            }),
        };
        if (!string.IsNullOrWhiteSpace(Options.AiApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Options.AiApiKey.Trim());
        }

        using var response = await Http.SendAsync(request, cancellationToken);
        using var document = await ReadAsync(response, cancellationToken);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    /// <summary>A JSON body with its length set (some proxies refuse chunked requests).</summary>
    private static StringContent Json(object body) => new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = body;
            try
            {
                using var error = JsonDocument.Parse(body);
                if (error.RootElement.TryGetProperty("error", out var e))
                {
                    detail = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var message) ? message.GetString() ?? body : e.ToString();
                }
            }
            catch (JsonException)
            {
            }

            throw new HttpRequestException($"The AI provider answered {(int)response.StatusCode}: {(detail.Length <= 200 ? detail : detail[..200])}");
        }

        return JsonDocument.Parse(body);
    }
}
