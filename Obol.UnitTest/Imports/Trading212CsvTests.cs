using System.Text;
using Obol.Library.Enums;
using Obol.WebApi.Features.Csv;

namespace Obol.UnitTest.Imports;

/// <summary>
/// The Trading 212 CSV export: read by its header (the columns change with what was ticked when exporting), every kind
/// of line, and imported twice without doubling anything.
/// </summary>
public sealed class Trading212CsvTests
{
    internal const string Export =
        "Action,Time,ISIN,Ticker,Name,Notes,ID,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result,Currency (Result),Total,Currency (Total),Withholding tax,Currency (Withholding tax),Currency conversion fee,Currency (Currency conversion fee)\n" +
        "Deposit,2025-01-02 09:15:00,,,,\"Bank Transfer\",dep-1,,,,,,,1000.00,EUR,,,,\n" +
        "Market buy,2025-01-03 14:30:12,IE00B4L5Y983,IWDA,\"iShares Core MSCI World UCITS ETF (Acc)\",,EOF1,5,90.50,EUR,1.00,,,452.50,EUR,,,,\n" +
        "Market buy,2025-01-06 15:31:00.123,US0378331005,AAPL,Apple,,EOF2,1.5,240.00,USD,0.96,,,346.25,EUR,,,0.52,EUR\n" +
        "Market sell,2025-03-10 15:00:00,US0378331005,AAPL,Apple,,EOF3,0.5,250.00,USD,0.92,4.10,EUR,114.90,EUR,,,0.17,EUR\n" +
        "Dividend (Dividend),2025-03-20 10:00:00,US0378331005,AAPL,Apple,,,1.0,0.25,USD,0.92,,,0.20,EUR,0.04,USD,,\n" +
        "Interest on cash,2025-03-31 23:59:59,,,,,int-1,,,,,,,1.23,EUR,,,,\n" +
        "Withdrawal,2025-04-01 09:00:00,,,,,wd-1,,,,,,,-100.00,EUR,,,,\n" +
        "Stock split open,2025-04-02 09:00:00,US0378331005,AAPL,Apple,,,1,1,USD,,,,0,EUR,,,,\n";

    [Fact]
    public void EveryKindOfLineIsRead()
    {
        var batch = Trading212Csv.Parse(Export, "EUR");

        Assert.NotNull(batch);
        Assert.Equal(3, batch.Trades.Count);
        Assert.Single(batch.Dividends);
        Assert.Equal(3, batch.Cash.Count);
        Assert.Equal(2, batch.Instruments.Count);
        Assert.Equal(1, batch.Skipped);
        Assert.Equal(1, batch.SkippedKinds["stock split"]);
        Assert.Equal("EUR", batch.Currency);
    }

    [Fact]
    public void TradesKeepSharesPriceTotalFeesAndTime()
    {
        var batch = Trading212Csv.Parse(Export, "EUR")!;

        var apple = batch.Trades.Single(static trade => trade.ExternalId == "EOF2");
        Assert.True(apple.Buy);
        Assert.Equal("US0378331005", apple.InstrumentKey);
        Assert.Equal(1.5m, apple.Quantity);
        Assert.Equal(240m, apple.Price);
        Assert.Equal("USD", apple.PriceCurrency);
        Assert.Equal(346.25m, apple.Total);
        Assert.Equal(0.52m, apple.Fees);
        Assert.Null(apple.RealizedPL);
        Assert.Equal(new DateTime(2025, 1, 6, 15, 31, 0, 123, DateTimeKind.Utc), apple.ExecutedAt);
        Assert.Equal(DateTimeKind.Utc, apple.ExecutedAt.Kind);
        Assert.Equal(DataSource.Csv, apple.Source);

        var sell = batch.Trades.Single(static trade => trade.ExternalId == "EOF3");
        Assert.False(sell.Buy);
        Assert.Equal(4.10m, sell.RealizedPL);
    }

    [Fact]
    public void InstrumentsGetTheirClassAndCurrencyFromTheFile()
    {
        var batch = Trading212Csv.Parse(Export, "EUR")!;

        var world = batch.Instruments["IE00B4L5Y983"];
        Assert.Equal("IWDA", world.Symbol);
        Assert.Equal("EUR", world.Currency);
        Assert.Equal((byte)AssetClass.EquityEtf, world.AssetClass);
        var apple = batch.Instruments["US0378331005"];
        Assert.Equal("USD", apple.Currency);
        Assert.Equal((byte)AssetClass.Stock, apple.AssetClass);
    }

    [Fact]
    public void DividendsAndTransfersHaveTheirSign()
    {
        var batch = Trading212Csv.Parse(Export, "EUR")!;

        var dividend = Assert.Single(batch.Dividends);
        Assert.Equal(0.20m, dividend.Amount);
        Assert.Equal(1.0m, dividend.Quantity);
        Assert.Equal(0.25m, dividend.PerShare);
        Assert.Null(dividend.Withheld);
        Assert.Equal("DIVIDEND", dividend.Kind);

        Assert.Equal(1000m, batch.Cash.Single(static movement => movement.Kind == CashKind.Deposit).Amount);
        Assert.Equal("Bank transfer", batch.Cash.Single(static movement => movement.Kind == CashKind.Deposit).Note);
        Assert.Equal(1.23m, batch.Cash.Single(static movement => movement.Kind == CashKind.Interest).Amount);
        Assert.Equal(-100m, batch.Cash.Single(static movement => movement.Kind == CashKind.Withdrawal).Amount);
    }

    [Fact]
    public void OlderExportsWithTheCurrencyInTheHeaderAreReadToo()
    {
        const string text =
            "﻿Action,Time (UTC),ISIN,Ticker,Name,No. of shares,Price / share,Currency (Price / share),Total (GBP)\r\n" +
            "Market buy,2024-05-02 08:00:01,PTGAL0AM0009,GALP,\"Galp Energia, SGPS\",10,15.10,EUR,130.00\r\n" +
            "Deposit,2024-05-01 08:00:00,,,,,,,500\r\n";

        var batch = Trading212Csv.Parse(text, "EUR");

        Assert.NotNull(batch);
        Assert.Equal("GBP", batch.Currency);
        var trade = Assert.Single(batch.Trades);
        Assert.Equal(130m, trade.Total);
        Assert.Equal(0m, trade.Fees);
        Assert.Equal("Galp Energia, SGPS", batch.Instruments["PTGAL0AM0009"].Name);
        Assert.Equal(500m, Assert.Single(batch.Cash).Amount);
    }

    [Fact]
    public void AFileThatIsNotAnExportIsRefused()
    {
        Assert.Null(Trading212Csv.Parse("Date,Amount\n2025-01-01,10\n", "EUR"));
        Assert.Null(Trading212Csv.Parse(string.Empty, "EUR"));
        Assert.True(Trading212Csv.Parse("Action,Time\n", "EUR")!.IsEmpty);
    }

    [Fact]
    public void UnreadableLinesAreCountedNotFatal()
    {
        const string text = "Action,Time,Total\nDeposit,yesterday,10\nDeposit,2025-01-01 10:00:00,0\nDeposit,2025-01-01 10:00:00,25\n";

        var batch = Trading212Csv.Parse(text, "EUR")!;

        Assert.Single(batch.Cash);
        Assert.Equal(2, batch.Skipped);
    }

    [Fact]
    public void QuotedCellsMayHoldCommasQuotesAndLineBreaks()
    {
        var rows = Trading212Csv.Rows("a,\"b, c\",\"say \"\"hi\"\"\"\n\"two\nlines\",x\n\n").ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "a", "b, c", "say \"hi\"" }, rows[0]);
        Assert.Equal(new[] { "two\nlines", "x" }, rows[1]);
    }

    [Fact]
    public async Task ImportingTheSameFileTwiceAddsNothingTheSecondTime()
    {
        var app = new TestApp();
        var userId = Guid.Parse(await app.CreateUserAsync());

        var first = await app.Writer.SaveAsync(userId, Trading212Csv.Parse(Export, "EUR")!);
        var second = await app.Writer.SaveAsync(userId, Trading212Csv.Parse(Export, "EUR")!);

        Assert.Equal((3, 1, 3), (first.Trades, first.Dividends, first.Cash));
        Assert.Equal((0, 0, 0), (second.Trades, second.Dividends, second.Cash));
        Assert.Equal(3, (await app.Portfolio.ListTradesAsync(userId)).Count);
    }

    [Fact]
    public async Task AnAccountImportedFromCsvShowsItsPositionsAndCash()
    {
        var app = new TestApp();
        var userId = await app.CreateUserAsync();
        await app.Writer.SaveAsync(Guid.Parse(userId), Trading212Csv.Parse(Export, "EUR")!);

        var holdings = await app.PortfolioService.HoldingsAsync(userId);

        Assert.True(holdings.IsSuccess);
        var positions = holdings.Result!.Positions;
        Assert.Equal(2, positions.Count);
        Assert.Equal(5m, positions.Single(static position => position.Instrument.Symbol == "IWDA").Quantity);
        var apple = positions.Single(static position => position.Instrument.Symbol == "AAPL");
        Assert.Equal(1m, apple.Quantity);
        Assert.Equal(230.83m, apple.Cost);
        Assert.Equal(1000m - 452.50m - 346.25m + 114.90m + 0.20m + 1.23m - 100m, holdings.Result.Cash);
    }

    [Fact]
    public void ImportKeysAreStableAndTellTradesApart()
    {
        var batch = Trading212Csv.Parse(Export, "EUR")!;
        var again = Trading212Csv.Parse(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(Export)), "EUR")!;

        var keys = batch.Trades.Select(Obol.WebApi.Features.Portfolio.Services.PortfolioWriter.TradeKey).ToList();

        Assert.Equal(keys, again.Trades.Select(Obol.WebApi.Features.Portfolio.Services.PortfolioWriter.TradeKey));
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Equal("US0378331005|20250106153100|B|1.5", keys[1]);
        Assert.Equal("US0378331005|20250320|0.20", Obol.WebApi.Features.Portfolio.Services.PortfolioWriter.DividendKey(batch.Dividends[0]));
    }

    // The 2026 layout: the time with its zone, stamp duty and Finra fee columns, card payments with the shop.
    internal const string Export2026 =
        "Action,Time (UTC),ISIN,Ticker,Name,Notes,ID,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result,Currency (Result),Total,Currency (Total),Withholding tax,Currency (Withholding tax),Stamp duty reserve tax,Currency (Stamp duty reserve tax),Finra fee,Currency conversion fee,Currency (Currency conversion fee),Merchant name,Merchant category,Currency (Finra fee)\n" +
        "Dividend (Dividend),2026-01-02 13:21:14+00:00,US0378331005,AAPL,\"Apple\",,,2.5000000000,0.260000,USD,0.85000000,,,0.55,\"EUR\",0.10,USD,,,,,,,,\n" +
        "Market buy,2026-01-13 16:37:31+00:00,US0378331005,AAPL,\"Apple\",,EOF100,1.5000000000,240.0000000000,USD,1.16000000,,,311.00,\"EUR\",,,,,,0.46,\"EUR\",,,\n" +
        "Limit sell,2026-02-23 14:37:24+00:00,PTGAL0AM0009,GALP,\"Galp Energia\",,EOF200,10.0000000000,21.5000000000,EUR,1.00000000,50.00,\"EUR\",215.00,\"EUR\",,,,,,,,,,\n" +
        "Withdrawal,2026-02-28 18:18:01+00:00,,,,\"Sent to Bank Account XXXX / PT00000000000000000000000\",w-1,,,,,,,-300.00,\"EUR\",,,,,,,,,,\n" +
        "Card debit,2026-03-18 12:56:16+00:00,,,,,c-1,,,,,,,-2.60,\"EUR\",,,,,,,,\"Corner Shop\",\"RETAIL_STORES\",\n" +
        "Spending cashback,2026-03-20 02:12:48+00:00,,,,,c-2,,,,,,,0.01,\"EUR\",,,,,,,,,,\n";

    [Fact]
    public void TheNewerExportWithZonedTimesAndCardPaymentsIsRead()
    {
        var batch = Trading212Csv.Parse(Export2026, "EUR");

        Assert.NotNull(batch);
        Assert.Equal(0, batch.Skipped);
        Assert.Equal(2, batch.Trades.Count);
        Assert.Single(batch.Dividends);
        Assert.Equal(3, batch.Cash.Count);
        Assert.Equal(new DateTime(2026, 1, 13, 16, 37, 31, DateTimeKind.Utc), batch.Trades[0].ExecutedAt);
        Assert.Equal(0.46m, batch.Trades[0].Fees);
        Assert.False(batch.Trades[1].Buy);
        Assert.Equal(50m, batch.Trades[1].RealizedPL);

        var bank = batch.Cash.Single(static movement => movement.Note == "To your bank account");
        Assert.Equal(CashKind.Withdrawal, bank.Kind);
        Assert.Equal(-300m, bank.Amount);
        var card = batch.Cash.Single(static movement => movement.Note == "Card · Corner Shop");
        Assert.Equal(CashKind.Withdrawal, card.Kind);
        Assert.Equal(-2.60m, card.Amount);
        var cashback = batch.Cash.Single(static movement => movement.Kind == CashKind.Other);
        Assert.Equal(0.01m, cashback.Amount);
    }

    [Theory]
    [InlineData("2026-01-02 13:21:14+00:00", 13)]
    [InlineData("2026-01-02 14:21:14+01:00", 13)]
    [InlineData("2026-01-02 13:21:14", 13)]
    [InlineData("2026-01-02 13:21:14.123", 13)]
    [InlineData("2026-01-02T13:21:14Z", 13)]
    public void TimesAreReadInEveryLayout(string text, int hour)
    {
        var at = Trading212Csv.ParseTime(text);

        Assert.NotNull(at);
        Assert.Equal(DateTimeKind.Utc, at.Value.Kind);
        Assert.Equal(new DateTime(2026, 1, 2, hour, 21, 14, DateTimeKind.Utc), at.Value.AddTicks(-(at.Value.Ticks % TimeSpan.TicksPerSecond)));
        Assert.Null(Trading212Csv.ParseTime("yesterday"));
    }
}
