using Obol.WebApi.Features.Portfolio.Services;
using Obol.WebApi.Features.Prices.Services;

namespace Obol.UnitTest.Prices;

/// <summary>
/// Finding an instrument on Yahoo Finance: symbols from the Trading 212 ticker, and prices in pence or pounds.
/// </summary>
public sealed class PriceServiceTests
{
    [Theory]
    [InlineData("AAPL_US_EQ", "AAPL")]
    [InlineData("BRK.B_US_EQ", "BRK-B")]
    [InlineData("VUSAl_EQ", "VUSA.L")]
    [InlineData("SAPd_EQ", "SAP.DE")]
    [InlineData("IWDAa_EQ", "IWDA.AS")]
    [InlineData("MCp_EQ", "MC.PA")]
    public void TickersBecomeYahooSymbols(string ticker, string expected) =>
        Assert.Equal(new[] { expected }, PriceService.TickerCandidates(ticker));

    [Theory]
    [InlineData("GALP_PT_EQ")]
    [InlineData("XYZq_EQ")]
    [InlineData("")]
    public void UnknownExchangesNeedASearch(string ticker) =>
        Assert.Empty(PriceService.TickerCandidates(ticker));

    [Fact]
    public void SearchResultsOnTheExchangesOfTheCurrencyComeFirst()
    {
        var quotes = new[]
        {
            new YahooQuote("IWDA.L", "LSE", "ETF"),
            new YahooQuote("IWDA.AS", "AMS", "ETF"),
            new YahooQuote("IWDA-OPT", "AMS", "OPTION"),
            new YahooQuote("iwda.as", "AMS", "ETF"),
        };

        Assert.Equal(new[] { "IWDA.AS", "IWDA.L" }, PriceService.Rank(quotes, "EUR"));
        Assert.Equal("IWDA.L", PriceService.Rank(quotes, "GBX")[0]);
    }

    [Theory]
    [InlineData("EUR", "EUR", 1.0)]
    [InlineData("GBp", "GBX", 1.0)]
    [InlineData("GBp", "GBP", 0.01)]
    [InlineData("GBP", "GBX", 100.0)]
    [InlineData("USD", "EUR", null)]
    public void PricesAreTurnedIntoTheInstrumentsCurrency(string yahoo, string instrument, double? expected) =>
        Assert.Equal(expected is { } value ? (decimal?)value : null, PriceService.Factor(yahoo, instrument));

    [Theory]
    [InlineData(100.0, "GBP", "GBX", 10000.0)]
    [InlineData(1500.0, "GBX", "GBP", 15.0)]
    [InlineData(12.5, "EUR", "EUR", 12.5)]
    [InlineData(12.5, "USD", "EUR", null)]
    public void TradePricesInPoundsOrPenceCountAsPrices(double price, string from, string to, double? expected) =>
        Assert.Equal(expected is { } value ? (decimal?)value : null, PortfolioLoader.SameCurrencyPrice((decimal)price, from, to));

    [Theory]
    [InlineData("2026-10-23", "2026-10-23")]
    [InlineData("2026-10-24", "2026-10-23")]
    [InlineData("2026-10-25", "2026-10-23")]
    [InlineData("2026-10-26", "2026-10-26")]
    public void WeekendPricesBelongToFriday(string day, string expected) =>
        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), PortfolioLoader.MarketDay(DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture)));

    private static YahooChart Chart(params (int Day, decimal Close)[] closes) =>
        new("USD", closes.Select(static item => new KeyValuePair<DateOnly, decimal>(new DateOnly(2026, 10, 1).AddDays(item.Day), item.Close)).ToList());

    [Fact]
    public void AListingOfAnotherCompanyIsTurnedDown()
    {
        // Archer Aviation still trades as ACIC_US_EQ on Trading 212; Yahoo's ACIC is another company, about three times the price.
        var acic = Chart((20, 11.02m), (21, 11.10m), (22, 10.95m));

        Assert.False(PriceService.Plausible(acic, 1m, new PriceReference(3.91m, new DateOnly(2026, 10, 23))));
        Assert.True(PriceService.Plausible(Chart((20, 3.80m), (21, 4.02m), (22, 3.95m)), 1m, new PriceReference(3.91m, new DateOnly(2026, 10, 23))));
    }

    [Fact]
    public void PenceAndAMissingReferencePass()
    {
        var pence = Chart((22, 7650m));

        Assert.True(PriceService.Plausible(pence, 0.01m, new PriceReference(76.9m, new DateOnly(2026, 10, 23))));
        Assert.True(PriceService.Plausible(pence, 1m, null));
        // Nothing near the reference day to compare with.
        Assert.True(PriceService.Plausible(pence, 1m, new PriceReference(1m, new DateOnly(2026, 6, 1))));
    }
}
