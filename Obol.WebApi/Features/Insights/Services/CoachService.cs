using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Obol.Library.Enums;
using Obol.Library.Services;
using Obol.WebApi.Extensions.Globalization;
using Obol.WebApi.Features.Insights.Models.DTOs;
using Obol.WebApi.Features.Portfolio.Services;

namespace Obol.WebApi.Features.Insights.Services;

/// <summary>The AI coach: reads the portfolio and writes a short review.</summary>
internal interface ICoachService
{
    /// <summary>The coach's state and last report.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The coach.</returns>
    Task<ServiceApiResponse<CoachDTO>> GetAsync(string userId);

    /// <summary>Starts a new review in the background (poll <see cref="GetAsync"/>).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The coach, running.</returns>
    Task<ServiceApiResponse<CoachDTO>> AnalyseAsync(string userId);
}

/// <summary>
/// Gathers the overview, holdings, income, exposure, performance and realized gains, sends them to the AI provider set
/// up in the options and keeps the last report in memory (one per account). In demo mode without a provider it shows
/// an example report, so the screen can be seen.
/// </summary>
/// <param name="portfolioService">Overview, holdings and income.</param>
/// <param name="insightsService">Exposure, performance and realized gains.</param>
/// <param name="modelClient">The AI provider.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="options">The module options.</param>
/// <param name="logger">Logger.</param>
internal sealed class CoachService(
    IPortfolioService portfolioService,
    IInsightsService insightsService,
    IAiModelClient modelClient,
    IGlobalizationHelper globalizationHelper,
    TimeProvider timeProvider,
    IOptions<ObolOptions> options,
    ILogger<CoachService> logger) : ICoachService
{
    private readonly ConcurrentDictionary<Guid, State> _states = new();

    /// <inheritdoc />
    public async Task<ServiceApiResponse<CoachDTO>> GetAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync(userId);
        }

        var state = _states.GetValueOrDefault(id);
        if (state is null && !modelClient.Enabled && options.Value.DemoMode)
        {
            state = new State { Report = Example(timeProvider.GetUtcNow()) };
        }

        return ServiceApiResponse<CoachDTO>.Success(ToDTO(state));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<CoachDTO>> AnalyseAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return await NotFoundAsync(userId);
        }

        if (!modelClient.Enabled)
        {
            return await GetAsync(userId);
        }

        var now = timeProvider.GetUtcNow();
        var state = _states.AddOrUpdate(
            id,
            _ => new State { Running = true, StartedAt = now },
            (_, old) => old.Running ? old : new State { Running = true, StartedAt = now, Report = old.Report });
        if (state.StartedAt == now)
        {
            _ = Task.Run(() => RunAsync(id, userId));
        }

        return ServiceApiResponse<CoachDTO>.Success(ToDTO(state));
    }

    /// <summary>Writes a review now (the background job; awaited by the tests).</summary>
    /// <param name="id">The account.</param>
    /// <param name="userId">The account, as the services take it.</param>
    /// <returns>The coach after it.</returns>
    internal async Task<CoachDTO> RunAsync(Guid id, string userId)
    {
        State finished;
        try
        {
            var overview = (await portfolioService.OverviewAsync(userId, ChartRange.Year)).Result ?? throw new InvalidOperationException("No overview.");
            var holdings = (await portfolioService.HoldingsAsync(userId)).Result ?? throw new InvalidOperationException("No holdings.");
            var income = (await portfolioService.IncomeAsync(userId, null)).Result ?? throw new InvalidOperationException("No income.");
            var exposure = (await insightsService.ExposureAsync(userId)).Result ?? throw new InvalidOperationException("No exposure.");
            var performance = (await insightsService.PerformanceAsync(userId, ChartRange.Year)).Result ?? throw new InvalidOperationException("No performance.");
            var realized = (await insightsService.RealizedAsync(userId, null)).Result ?? throw new InvalidOperationException("No realized gains.");

            var input = new CoachInput(overview, holdings, income, exposure, performance, realized);
            var answer = await modelClient.CompleteAsync(CoachPrompt.System(options.Value.AiLanguage), CoachPrompt.Data(input), CancellationToken.None);
            var report = CoachPrompt.Parse(answer);
            report.GeneratedAt = timeProvider.GetUtcNow();
            finished = new State { Report = report };
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Coach: review written by {Provider} {Model}", modelClient.Provider, modelClient.Model);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Coach: the review failed");
            finished = new State
            {
                Report = _states.GetValueOrDefault(id)?.Report,
                Error = exception is HttpRequestException or TaskCanceledException
                    ? $"Couldn't reach the AI ({modelClient.Provider}): {exception.Message}"
                    : "The review failed. Try again in a minute.",
            };
        }

        _states[id] = finished;
        return ToDTO(finished);
    }

    /// <summary>The demo's example review (no AI provider involved).</summary>
    /// <param name="at">When.</param>
    /// <returns>The report.</returns>
    internal static CoachReportDTO Example(DateTimeOffset at) => new()
    {
        GeneratedAt = at,
        Example = true,
        Headline = "Solid core, a few bets doing the heavy lifting",
        Summary = "Your world ETF and the dividend ETF carry most of the portfolio, which keeps it well spread. Over the last year you grew a little faster than the money you put in, but slightly behind MSCI World, mostly because the bond ETF lagged while shares rallied.",
        Strengths =
        [
            "IWDA and VHYL are over half of what's invested: thousands of companies under two tickers.",
            "Monthly deposits went in on schedule: the money-weighted return beats what timing alone would give.",
            "Income keeps growing: the next 12 months should pay more than the last 12.",
        ],
        Watch =
        [
            "NVDA alone has more than doubled since you bought it; it's now a bigger bet than you planned.",
            "About a fifth of what's invested is in dollars: a 5 % stronger euro would take a few hundred euros off.",
            "Tax withheld at source on the US dividends is lost unless you claim it in your tax return (Anexo J).",
        ],
        Actions =
        [
            "Decide how big you want any single share to get, and check NVDA and AAPL against it.",
            "Look at whether the bond ETF still has the job you gave it, now that rates have moved.",
            "Write down the withholding of this year's US dividends for your tax return.",
        ],
    };

    private CoachDTO ToDTO(State? state) => new()
    {
        Enabled = modelClient.Enabled,
        Provider = modelClient.Provider,
        Model = modelClient.Enabled ? modelClient.Model : string.Empty,
        Running = state?.Running == true,
        StartedAt = state?.Running == true ? state.StartedAt : null,
        Error = state?.Error ?? string.Empty,
        Report = state?.Report,
    };

    private async Task<ServiceApiResponse<CoachDTO>> NotFoundAsync(string? what) =>
        ServiceApiResponse<CoachDTO>.NotFound(await globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), what ?? string.Empty));

    private sealed class State
    {
        public bool Running { get; init; }

        public DateTimeOffset? StartedAt { get; init; }

        public string Error { get; init; } = string.Empty;

        public CoachReportDTO? Report { get; init; }
    }
}
