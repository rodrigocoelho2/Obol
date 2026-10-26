using Obol.Library.Enums;
using Obol.WebApi.Features.Trading212;

namespace Obol.UnitTest.Imports;

/// <summary>
/// From the Trading 212 API to Obol's records: instruments (with the catalogue filling what an order leaves out),
/// fills, dividends and transfers.
/// </summary>
public sealed class Trading212MapperTests
{
    private static readonly DateTimeOffset Filled = new(2025, 6, 2, 13, 45, 10, TimeSpan.Zero);

    [Theory]
    [InlineData("GBp", "GBX")]
    [InlineData("usd", "USD")]
    [InlineData("EUR", "EUR")]
    [InlineData(null, "EUR")]
    [InlineData("EURO", "EUR")]
    public void CurrenciesAreThreeLettersAndPenceAreGbx(string? currency, string expected) =>
        Assert.Equal(expected, Trading212Mapper.Currency(currency));

    [Fact]
    public void TheCatalogueFillsTheIsinTheNameTheCurrencyAndTheType()
    {
        var catalogue = new Dictionary<string, T212TradableInstrument>
        {
            ["VUSAl_EQ"] = new() { Ticker = "VUSAl_EQ", Isin = "IE00B3XXRP09", Name = "Vanguard S&P 500 UCITS ETF (Dist)", CurrencyCode = "GBP", Type = "ETF" },
        };

        var instrument = Trading212Mapper.Instrument(new T212Instrument { Ticker = "VUSAl_EQ" }, null, null, catalogue);

        Assert.NotNull(instrument);
        Assert.Equal("IE00B3XXRP09", instrument.Key);
        Assert.Equal("IE00B3XXRP09", instrument.Isin);
        Assert.Equal("VUSAl_EQ", instrument.Ticker);
        Assert.Equal("VUSA", instrument.Symbol);
        Assert.Equal("GBP", instrument.Currency);
        Assert.Equal((byte)AssetClass.EquityEtf, instrument.AssetClass);
    }

    [Fact]
    public void WithoutAnIsinTheTickerIsTheKey()
    {
        var instrument = Trading212Mapper.Instrument(null, "XYZ_US_EQ", "USD", null);

        Assert.NotNull(instrument);
        Assert.Equal("T212:XYZ_US_EQ", instrument.Key);
        Assert.Null(instrument.Isin);
        Assert.Equal("XYZ", instrument.Symbol);
        Assert.Equal("USD", instrument.Currency);
        Assert.Null(Trading212Mapper.Instrument(null, null, "USD", null));
    }

    [Fact]
    public void ABuyFillKeepsItsTotalFeesAndRate()
    {
        var order = new T212HistoricalOrder
        {
            Order = new T212Order { Id = 9, Side = "BUY" },
            Fill = new T212Fill
            {
                Id = 77,
                Quantity = 2,
                Price = 100m,
                FilledAt = Filled,
                Type = "TRADE",
                WalletImpact = new T212FillWallet { NetValue = -185.5m, FxRate = 0.92m, Taxes = [new T212Tax { Name = "CURRENCY_CONVERSION_FEE", Quantity = -0.3m }] },
            },
        };

        var trade = Trading212Mapper.Trade(order, "US0378331005", "USD");

        Assert.NotNull(trade);
        Assert.True(trade.Buy);
        Assert.Equal(2m, trade.Quantity);
        Assert.Equal(185.5m, trade.Total);
        Assert.Equal(0.3m, trade.Fees);
        Assert.Equal(0.92m, trade.FxRate);
        Assert.Null(trade.RealizedPL);
        Assert.Equal("77", trade.ExternalId);
        Assert.Equal(Filled.UtcDateTime, trade.ExecutedAt);
        Assert.Equal(DataSource.Trading212, trade.Source);
    }

    [Fact]
    public void ASellKeepsItsResult()
    {
        var order = new T212HistoricalOrder
        {
            Order = new T212Order { Id = 10, Side = "SELL" },
            Fill = new T212Fill { Id = 0, Quantity = 1, Price = 210m, FilledAt = Filled, WalletImpact = new T212FillWallet { NetValue = 210m, RealisedProfitLoss = 12.5m } },
        };

        var trade = Trading212Mapper.Trade(order, "IE00B4L5Y983", "EUR");

        Assert.NotNull(trade);
        Assert.False(trade.Buy);
        Assert.Equal(12.5m, trade.RealizedPL);
        Assert.Equal("10", trade.ExternalId);
    }

    [Fact]
    public void FillsThatAreNotTradesAreLeftOut()
    {
        var split = new T212HistoricalOrder { Fill = new T212Fill { Quantity = 3, Price = 1m, FilledAt = Filled, Type = "STOCK_SPLIT" } };
        var unfilled = new T212HistoricalOrder { Order = new T212Order { Side = "BUY" } };

        Assert.Null(Trading212Mapper.Trade(split, "X", "EUR"));
        Assert.Null(Trading212Mapper.Trade(unfilled, "X", "EUR"));
    }

    [Fact]
    public void WithoutAWalletImpactTheTotalIsSharesTimesPrice()
    {
        var order = new T212HistoricalOrder { Order = new T212Order { Side = "BUY" }, Fill = new T212Fill { Quantity = 4, Price = 2.5m, FilledAt = Filled } };

        Assert.Equal(10m, Trading212Mapper.Trade(order, "X", "EUR")!.Total);
    }

    [Theory]
    [InlineData("DEPOSIT", 100, CashKind.Deposit, 100)]
    [InlineData("WITHDRAW", 50, CashKind.Withdrawal, -50)]
    [InlineData("INTEREST_ON_FREE_CASH", 0.42, CashKind.Interest, 0.42)]
    [InlineData("FEE", 1, CashKind.Fee, -1)]
    [InlineData("TRANSFER", -20, CashKind.Other, -20)]
    public void TransfersHaveTheirKindAndSign(string type, double amount, CashKind kind, double expected)
    {
        var cash = Trading212Mapper.Cash(new T212Transaction { Type = type, Amount = (decimal)amount, DateTime = Filled });

        Assert.NotNull(cash);
        Assert.Equal(kind, cash.Kind);
        Assert.Equal((decimal)expected, cash.Amount);
    }

    [Fact]
    public void UnknownOrEmptyTransfersAreLeftOut()
    {
        Assert.Null(Trading212Mapper.Cash(new T212Transaction { Type = "SOMETHING_NEW", Amount = 5m, DateTime = Filled }));
        Assert.Null(Trading212Mapper.Cash(new T212Transaction { Type = "DEPOSIT", Amount = 0m, DateTime = Filled }));
    }

    [Fact]
    public void DividendsKeepTheirSharesAndKind()
    {
        var dividend = Trading212Mapper.Dividend(new T212Dividend { Amount = 3.21m, Quantity = 0, GrossAmountPerShare = 0.4m, PaidOn = Filled, Type = " ordinary " }, "IE00B8GKDB10");
        var plain = Trading212Mapper.Dividend(new T212Dividend { Amount = 1m, Quantity = 12, PaidOn = Filled }, "IE00B8GKDB10");

        Assert.Null(dividend.Quantity);
        Assert.Equal(0.4m, dividend.PerShare);
        Assert.Equal("ORDINARY", dividend.Kind);
        Assert.Equal(12m, plain.Quantity);
        Assert.Equal("DIVIDEND", plain.Kind);
    }

    [Fact]
    public void PiesComeWithTheirNameMoneyAndTheSharesTheyOwn()
    {
        var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var pies = System.Text.Json.JsonSerializer.Deserialize<List<T212Pie>>("""
            [{"id": 4101, "cash": 0.42, "dividendDetails": {"gained": 12.5, "inCash": 0.4, "reinvested": 12.1}, "progress": 0.31,
              "result": {"priceAvgInvestedValue": 2186.05, "priceAvgResult": 399.3, "priceAvgResultCoef": 0.1828, "priceAvgValue": 2585.35}, "status": "AHEAD"}]
            """, web)!;
        var detail = System.Text.Json.JsonSerializer.Deserialize<T212PieDetail>("""
            {"instruments": [
               {"ticker": "NVDA_US_EQ", "ownedQuantity": 3.6894, "currentShare": 0.24, "expectedShare": 0.25,
                "result": {"priceAvgInvestedValue": 490.9, "priceAvgResult": 121.5, "priceAvgResultCoef": 0.247, "priceAvgValue": 612.4}, "issues": []},
               {"ticker": "GONE_US_EQ", "ownedQuantity": 1, "result": {"priceAvgInvestedValue": 10, "priceAvgValue": 9}},
               {"ticker": "AAPL_US_EQ", "ownedQuantity": 0, "result": {"priceAvgInvestedValue": 0, "priceAvgValue": 0}}],
             "settings": {"id": 4101, "name": "Pelosi Tracker", "icon": "Rocket", "goal": 0, "creationDate": "2024-03-01T10:00:00Z",
               "dividendCashAction": "REINVEST", "instrumentShares": {"NVDA_US_EQ": 0.25}}}
            """, web)!;

        var pie = Trading212Mapper.Pie(pies[0], detail);
        var owned = Trading212Mapper.PieHoldings(4101, detail, ticker => ticker == "NVDA_US_EQ" ? 27 : ticker == "AAPL_US_EQ" ? 28 : null);

        Assert.Equal(4101, pie.PieID);
        Assert.Equal("Pelosi Tracker", pie.Name);
        Assert.Equal("Rocket", pie.Icon);
        Assert.Equal(0.42m, pie.Cash);
        Assert.Equal(2186.05m, pie.Invested);
        Assert.Equal(2585.35m, pie.Value);
        Assert.Equal(399.3m, pie.Result);
        Assert.Equal(12.5m, pie.DividendsGained);
        Assert.Null(pie.Goal);
        Assert.Equal("2024-03-01T10:00:00", pie.CreatedAt);
        // An instrument Obol doesn't hold, or that the pie owns none of, is left out.
        var nvidia = Assert.Single(owned);
        Assert.Equal(27, nvidia.InstrumentID);
        Assert.Equal(3.6894m, nvidia.Quantity);
        Assert.Equal(490.9m, nvidia.Invested);
        Assert.Equal(612.4m, nvidia.Value);
        Assert.Equal(0.24m, nvidia.CurrentShare);
        Assert.Equal(0.25m, nvidia.ExpectedShare);
    }

    [Fact]
    public void APieWithoutItsDetailsIsNamedByItsId()
    {
        var pie = Trading212Mapper.Pie(new T212Pie { Id = 77, Result = new T212PieResult { PriceAvgValue = 10m } }, null);

        Assert.Equal("Pie 77", pie.Name);
        Assert.Equal(string.Empty, pie.Icon);
        Assert.Equal(10m, pie.Value);
    }

    [Theory]
    [InlineData("1709287200.0")]
    [InlineData("1709287200000")]
    [InlineData("\"2024-03-01T10:00:00Z\"")]
    [InlineData("\"2024-03-01T10:00:00.000+00:00\"")]
    public void APieDateCanBeSecondsMillisecondsOrIso(string creationDate)
    {
        var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var detail = System.Text.Json.JsonSerializer.Deserialize<T212PieDetail>(
            """{"instruments": [{"ticker": "TSLA_US_EQ", "ownedQuantity": 0.25, "currentShare": null, "result": {"priceAvgValue": 99.5, "priceAvgInvestedValue": null}}], "settings": {"id": 9, "name": "Mag 7", "goal": null, "endDate": null, "creationDate": """
            + creationDate + "}}", web)!;

        var pie = Trading212Mapper.Pie(new T212Pie { Id = 9, Cash = null }, detail);
        var owned = Assert.Single(Trading212Mapper.PieHoldings(9, detail, static _ => 3));

        Assert.Equal("2024-03-01T10:00:00", pie.CreatedAt);
        Assert.Equal(0m, pie.Cash);
        Assert.Equal(99.5m, owned.Value);
        Assert.Equal(0m, owned.Invested);
        Assert.Null(owned.CurrentShare);
    }
}
