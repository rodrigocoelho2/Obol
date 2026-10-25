using Obol.Library.Enums;
using Obol.WebApi.Features.Portfolio.Services;
using static Obol.UnitTest.TestSupport;

namespace Obol.UnitTest.Portfolio;

/// <summary>
/// The price book (prices carried forward, exchange rates, pence) and the income maths (what came in, what should come).
/// </summary>
public sealed class PriceBookAndIncomeTests
{
    [Fact]
    public void APriceIsCarriedForwardOverDaysWithoutAClose()
    {
        var book = new PriceBook("EUR");
        book.AddPrice(1, Today.AddDays(-5), 10m);
        book.AddPrice(1, Today.AddDays(-2), 12m);

        Assert.Equal(10m, book.PriceOn(1, Today.AddDays(-4)));
        Assert.Equal(12m, book.PriceOn(1, Today));
        Assert.Equal(10m, book.PriceOn(1, Today.AddDays(-30)));
        Assert.Null(book.PriceOn(2, Today));
        Assert.Equal((Today.AddDays(-2), 12m, Today.AddDays(-5), 10m), book.LastTwo(1, Today));
    }

    [Fact]
    public void ADailyCloseWinsOverATradePriceOfTheSameDay()
    {
        var book = new PriceBook("EUR");
        book.AddPrice(1, Today, 10m);
        book.AddPrice(1, Today, 11m, replace: false);
        book.AddPrice(1, Today.AddDays(-1), 0m);

        Assert.Equal(10m, book.PriceOn(1, Today));
        Assert.Single(book.Series(1));
    }

    [Fact]
    public void PenceArePoundsOverAHundred()
    {
        var book = new PriceBook("EUR");
        book.AddFx("GBP", Today, 1.15m);

        Assert.Equal(1m, book.FxOn("EUR", Today));
        Assert.Equal(1m, book.FxOn("eur", Today));
        Assert.Equal(1.15m, book.FxOn("GBP", Today));
        Assert.Equal(0.0115m, book.FxOn("GBX", Today));
        Assert.True(book.HasFx("GBX"));
        Assert.False(book.HasFx("USD"));
        Assert.Equal(0.01m, new PriceBook("GBP").FxOn("GBX", Today));
    }

    [Fact]
    public void TheNextPaymentsRepeatTheLastYearOnTheSharesHeldToday()
    {
        var data = Data(
            [Instrument(1, "VHYL"), Instrument(2, "GALP", assetClass: AssetClass.Stock)],
            trades: [Buy(1, Today.AddDays(-200), 10, 60m), Buy(1, Today.AddDays(-50), 10, 65m), Buy(2, Today.AddDays(-300), 5, 15m), Sell(2, Today.AddDays(-20), 5, 18m)],
            dividends: [Dividend(1, Today.AddDays(-90), 10m, quantity: 10m), Dividend(1, Today.AddDays(-400), 9m, quantity: 10m), Dividend(2, Today.AddDays(-60), 3m)]);

        var expected = IncomeMath.Expected(data, PortfolioMath.Positions(data));

        var payment = Assert.Single(expected);
        Assert.Equal(1, payment.InstrumentId);
        Assert.Equal(Today.AddDays(-90).AddYears(1), payment.Day);
        Assert.Equal(20m, payment.Amount);
    }

    [Fact]
    public void WithoutTheSharesOfAPaymentTheTradesTellThem()
    {
        var data = Data(
            [Instrument(1, "VHYL")],
            trades: [Buy(1, Today.AddDays(-200), 4, 60m), Buy(1, Today.AddDays(-10), 4, 65m)],
            dividends: [Dividend(1, Today.AddDays(-100), 2m)]);

        var payment = Assert.Single(IncomeMath.Expected(data, PortfolioMath.Positions(data)));

        Assert.Equal(4m, payment.Amount);
    }

    [Fact]
    public void ReceivedHasTheDividendsAndTheInterestOldestFirst()
    {
        var data = Data(
            [Instrument(1, "VHYL")],
            cash: [Cash(CashKind.Interest, Today.AddDays(-3), 0.5m), Cash(CashKind.Deposit, Today.AddDays(-30), 100m)],
            dividends: [Dividend(1, Today.AddDays(-10), 2m)]);

        var received = IncomeMath.Received(data);

        Assert.Equal(2, received.Count);
        Assert.Equal(1, received[0].InstrumentId);
        Assert.Null(received[1].InstrumentId);
        Assert.Equal(0.5m, received[1].Amount);
    }
}
