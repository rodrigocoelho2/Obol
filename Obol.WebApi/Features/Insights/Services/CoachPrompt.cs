using System.Globalization;
using System.Text;
using System.Text.Json;
using Obol.WebApi.Features.Insights.Models.DTOs;
using Obol.WebApi.Features.Portfolio.Models.DTOs;

namespace Obol.WebApi.Features.Insights.Services;

/// <summary>Everything the coach reads about a portfolio.</summary>
/// <param name="Overview">Net worth, returns, allocation.</param>
/// <param name="Holdings">The positions.</param>
/// <param name="Income">The income of this year and by position.</param>
/// <param name="Exposure">Currencies, sectors, countries, concentration.</param>
/// <param name="Performance">The last year against the indexes.</param>
/// <param name="Realized">This year's sales.</param>
internal sealed record CoachInput(OverviewDTO Overview, HoldingsDTO Holdings, IncomeDTO Income, ExposureDTO Exposure, PerformanceDTO Performance, RealizedDTO Realized);

/// <summary>
/// The instructions and the data the AI coach gets, and reading its answer.
/// </summary>
internal static class CoachPrompt
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The instructions.</summary>
    /// <param name="language">The language to write in.</param>
    /// <returns>The system prompt.</returns>
    public static string System(string language) => $$"""
        You are the portfolio coach inside Obol, a self-hosted tracker of a Trading 212 account. You read the numbers you
        are given about one person's portfolio and write a short, plain review for them.

        Look at, in this order of importance:
        - Concentration: the biggest positions' weights, the top 5, the effective number of positions, overlapping funds.
        - Repeated bets: several positions in the same sector, country or theme (for example many BDCs, REITs, chip makers).
        - Income: yields that look too high to last, payments per share that were cut, tax withheld at source, how much
          of the income depends on one or two names.
        - Currency: how much is in dollars or other currencies and what the exchange rate did.
        - Performance: the time-weighted and money-weighted return against MSCI World and the S&P 500.
        - Realized gains and losses this year.

        Rules:
        - Write in {{language}}. Talk to the person directly ("you"). Quote their numbers and their symbols.
        - You are not a financial adviser: never tell them to buy or sell a specific security. You may describe risks and
          general options (spreading out, a broad index fund, checking a company's payout) for them to think about.
        - Don't invent data you weren't given. If something is missing, say so briefly.
        - Answer with JSON only, no other text, in this shape:
          {"headline": "at most 10 words", "summary": "2 or 3 sentences", "strengths": ["up to 3 short points"],
           "watch": ["up to 4 short points"], "actions": ["exactly 3 concrete things to look at in the next weeks"]}
        """;

    /// <summary>The portfolio, as plain text.</summary>
    /// <param name="input">The data.</param>
    /// <returns>The user prompt.</returns>
    public static string Data(CoachInput input)
    {
        var o = input.Overview;
        var c = o.Currency;
        var text = new StringBuilder();
        text.AppendLine(Invariant, $"Currency: {c}. Net worth {M(o.TotalValue)}, of which cash {M(o.Cash)}. Cost of the positions {M(o.Cost)}, unrealised {M(o.Unrealized)} ({P(o.UnrealizedPct)}).");
        text.AppendLine(Invariant, $"Money put in (net): {(o.NetDeposits is { } put ? M(put) : "unknown")}. Total return {(o.TotalReturn is { } total ? M(total) : "unknown")} ({P(o.TotalReturnPct)}).");
        text.AppendLine(Invariant, $"Allocation: {string.Join(", ", o.Allocation.Select(slice => $"{slice.Label} {P(slice.Percent)}"))}.");

        var p = input.Performance;
        if (p.HasData)
        {
            text.AppendLine(Invariant, $"Last {p.Days} days: time-weighted {P(p.TimeWeighted)}, money-weighted {P(p.MoneyWeighted)} ({P(p.MoneyWeightedYearly)} a year).");
            foreach (var benchmark in p.Benchmarks.Where(static benchmark => benchmark.Available))
            {
                text.AppendLine(Invariant, $"- {benchmark.Label} over the same days: {P(benchmark.Return)}; the same money in it would be worth {M(benchmark.Value)} (you: {M(p.Value)}).");
            }
        }

        var e = input.Exposure;
        text.AppendLine(Invariant, $"Concentration: {e.Concentration.Positions} positions, biggest {e.Concentration.TopSymbol} {P(e.Concentration.TopWeight)}, top 5 {P(e.Concentration.Top5Weight)}, effective positions {e.Concentration.Effective.ToString("0.0", Invariant)}.");
        text.AppendLine(Invariant, $"Currencies: {string.Join(", ", e.Currencies.Select(slice => $"{slice.Key} {P(slice.Percent)}{(slice.FxImpact is { } fx ? $" (exchange rate effect {M(fx)})" : string.Empty)}"))}.");
        text.AppendLine(Invariant, $"Sectors (of what's invested): {string.Join(", ", e.Sectors.Select(slice => $"{slice.Label} {P(slice.Percent)} [{string.Join(" ", slice.Symbols)}]"))}.");
        text.AppendLine(Invariant, $"Countries: {string.Join(", ", e.Countries.Select(slice => $"{slice.Label} {P(slice.Percent)}"))}.");

        text.AppendLine("Positions (symbol, name, class, weight, value, return since bought):");
        foreach (var position in input.Holdings.Positions.Take(40))
        {
            text.AppendLine(Invariant, $"- {position.Instrument.Symbol}, {position.Instrument.Name}, {position.Instrument.Class}, {P(position.Weight)}, {M(position.Value)}, {P(position.UnrealizedPct)}");
        }

        var i = input.Income;
        text.AppendLine(Invariant, $"Income {i.Year}: received {M(i.Received)} (interest {M(i.Interest)}); next 12 months about {M(i.Next12)}; yield on cost {P(i.YieldOnCost)}; tax withheld at source in the last 12 months {M(i.Withheld)}.");
        foreach (var item in i.Positions.Take(25))
        {
            text.AppendLine(Invariant, $"- {item.Instrument.Symbol}: {M(item.Received)} in 12 months, yield {P(item.Yield)}, {item.Payments} payments{(item.Trend.Length > 0 ? $", last payment {item.Trend} {P(item.Change)}" : string.Empty)}, withheld {M(item.Withheld)}");
        }

        var r = input.Realized;
        text.AppendLine(Invariant, $"Sales in {r.Year}: {r.Sales.Count}, realized {M(r.Gain)} ({r.Wins} with a gain, {r.Losses} with a loss).");
        return text.ToString();

        string M(decimal? value) => value is { } number ? number.ToString("#,0.00", Invariant) + " " + c : "-";
    }

    /// <summary>Reads the model's answer (JSON, or plain text as a fallback).</summary>
    /// <param name="answer">The answer.</param>
    /// <returns>The report.</returns>
    public static CoachReportDTO Parse(string answer)
    {
        var text = (answer ?? string.Empty).Trim();
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                using var document = JsonDocument.Parse(text[start..(end + 1)]);
                var root = document.RootElement;
                return new CoachReportDTO
                {
                    Headline = Text(root, "headline"),
                    Summary = Text(root, "summary"),
                    Strengths = List(root, "strengths"),
                    Watch = List(root, "watch"),
                    Actions = List(root, "actions"),
                };
            }
            catch (JsonException)
            {
                // Not JSON after all: show it as it is.
            }
        }

        return new CoachReportDTO { Headline = "Your portfolio review", Summary = text };
    }

    private static string P(decimal? value) => value is { } number ? (number * 100m).ToString("0.0", Invariant) + "%" : "-";

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;

    private static List<string> List(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(static item => item.ValueKind == JsonValueKind.String).Select(static item => item.GetString()!.Trim()).Where(static item => item.Length > 0).Take(5).ToList()
            : [];
}
