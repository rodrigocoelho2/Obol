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
}
