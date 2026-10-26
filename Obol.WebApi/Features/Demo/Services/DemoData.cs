using Obol.Library.Enums;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Instruments.Services;
using Obol.WebApi.Features.Portfolio.Services;

namespace Obol.WebApi.Features.Demo.Services;

/// <summary>
/// The sample portfolio of demo mode: two years of a monthly €200 into a world ETF, quarterly dividend ETF buys, a bond
/// ETF, a few shares (one partly sold), monthly deposits, dividends, interest, and daily prices made by a seeded random
/// walk that ends at today's prices. The same seed always gives the same portfolio.
/// </summary>
internal static class DemoData
{
    /// <summary>The account currency of the demo.</summary>
    public const string Currency = "EUR";

    /// <summary>How far back the demo goes.</summary>
    public const int Years = 2;

    private static readonly DemoInstrument[] Catalogue =
    [
        new("IE00B4L5Y983", "IWDAa_EQ", "iShares Core MSCI World UCITS ETF (Acc)", "EUR", AssetClass.EquityEtf, 104.62, 0.12, 0.009, 0, 0),
        new("IE00B8GKDB10", "VHYLa_EQ", "Vanguard FTSE All-World High Dividend Yield UCITS ETF (Dist)", "EUR", AssetClass.EquityEtf, 69.69, 0.09, 0.008, 0.032, 4),
        new("IE00B4WXJJ64", "IEGAa_EQ", "iShares Core € Govt Bond UCITS ETF (Dist)", "EUR", AssetClass.BondEtf, 111.91, 0.012, 0.003, 0.022, 2),
        new("PTGAL0AM0009", "GALP_PT_EQ", "Galp Energia SGPS", "EUR", AssetClass.Stock, 18.21, -0.04, 0.016, 0.031, 2),
        new("US67066G1040", "NVDA_US_EQ", "NVIDIA", "USD", AssetClass.Stock, 188.0, 0.42, 0.027, 0.0002, 4),
        new("US0378331005", "AAPL_US_EQ", "Apple", "USD", AssetClass.Stock, 236.0, 0.14, 0.015, 0.0044, 4),
    ];

    /// <summary>
    /// The demo's Trading 212 pies: each owns a share of some positions (what's left is held outside the pies).
    /// </summary>
    public static readonly IReadOnlyList<(long Id, string Name, string Icon, decimal? Goal, (string Isin, decimal Share)[] Holdings)> Pies =
    [
        (4101, "Long term", "Retirement", 50000m, new (string, decimal)[] { ("IE00B4L5Y983", 0.8m), ("IE00B4WXJJ64", 1m) }),
        (4102, "Big tech", "Rocket", null, new (string, decimal)[] { ("US67066G1040", 1m), ("US0378331005", 0.7m) }),
    ];

    /// <summary>
    /// Builds the demo.
    /// </summary>
    /// <param name="today">Today.</param>
    /// <param name="seed">The random seed.</param>
    /// <returns>The trades, dividends, cash, prices and exchange rates.</returns>
    public static DemoPortfolio Generate(DateOnly today, int seed = 20261025)
    {
        var random = new Random(seed);
        var start = today.AddYears(-Years);
        var days = new List<DateOnly>();
        for (var day = start; day <= today; day = day.AddDays(1))
        {
            if (day.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                days.Add(day);
            }
        }

        // Prices: a walk backwards from today's price, so the demo always ends where the mock does.
        var prices = new Dictionary<string, SortedList<DateOnly, decimal>>(StringComparer.Ordinal);
        foreach (var instrument in Catalogue)
        {
            prices[instrument.Isin] = Walk(days, instrument.Price, instrument.Drift, instrument.Volatility, random);
        }

        var usdEur = Walk(days, 0.92, -0.01, 0.004, random);

        // The benchmarks: the euro listings of MSCI World and S&P 500 ETFs.
        var benchmarkRandom = new Random(seed + 1);
        var benchmarks = new Dictionary<string, SortedList<DateOnly, decimal>>(StringComparer.Ordinal)
        {
            [Benchmarks.World.Key] = Walk(days, 103.4, 0.07, 0.0085, benchmarkRandom),
            [Benchmarks.SP500.Key] = Walk(days, 628.0, 0.06, 0.0105, benchmarkRandom),
        };

        var batch = new ImportBatch { Currency = Currency };
        foreach (var instrument in Catalogue)
        {
            batch.Add(new InstrumentInput(
                instrument.Isin, instrument.Isin, instrument.Ticker, InstrumentNames.Symbol(instrument.Ticker),
                instrument.Name, instrument.Currency, (byte)instrument.Class));
        }

        var held = Catalogue.ToDictionary(static instrument => instrument.Isin, static _ => 0m, StringComparer.Ordinal);
        var cash = 0m;
        DateOnly Trading(DateOnly day)
        {
            while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                day = day.AddDays(1);
            }

            return day;
        }

        decimal Fx(string currency, DateOnly day) => currency == "USD" ? At(usdEur, day) : 1m;
        DateTime When(DateOnly day, int hour, int minute) => DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Utc);

        void Deposit(DateOnly day, decimal amount)
        {
            cash += amount;
            batch.Cash.Add(new ImportedCash(CashKind.Deposit, amount, When(day, 7, 30), "Bank transfer", DataSource.Demo));
        }

        void Buy(DemoInstrument instrument, DateOnly day, decimal euros, int minute)
        {
            if (day > today)
            {
                return;
            }

            var price = At(prices[instrument.Isin], day);
            var fx = Fx(instrument.Currency, day);
            var quantity = Math.Round(euros / (price * fx), 6);
            var total = Math.Round(quantity * price * fx, 2);
            held[instrument.Isin] += quantity;
            cash -= total;
            batch.Trades.Add(new ImportedTrade(instrument.Isin, true, quantity, price, instrument.Currency, instrument.Currency == Currency ? null : fx, total, null, 0m, When(day, 9, minute), DataSource.Demo, null));
        }

        void Sell(DemoInstrument instrument, DateOnly day, decimal share)
        {
            var price = At(prices[instrument.Isin], day);
            var fx = Fx(instrument.Currency, day);
            var quantity = Math.Round(held[instrument.Isin] * share, 6);
            var total = Math.Round(quantity * price * fx, 2);
            var cost = batch.Trades.Where(trade => trade.InstrumentKey == instrument.Isin && trade.Buy).Sum(static trade => trade.Total)
                / Math.Max(0.000001m, batch.Trades.Where(trade => trade.InstrumentKey == instrument.Isin && trade.Buy).Sum(static trade => trade.Quantity));
            held[instrument.Isin] -= quantity;
            cash += total;
            batch.Trades.Add(new ImportedTrade(instrument.Isin, false, quantity, price, instrument.Currency, null, total, Math.Round(total - cost * quantity, 2), 0m, When(day, 14, 12), DataSource.Demo, null));
        }

        var world = Catalogue[0];
        var dividend = Catalogue[1];
        var bonds = Catalogue[2];
        var galp = Catalogue[3];
        var nvidia = Catalogue[4];
        var apple = Catalogue[5];

        Deposit(Trading(start), 2000m);
        var month = 0;
        for (var first = new DateOnly(start.Year, start.Month, 1).AddMonths(1); first <= today; first = first.AddMonths(1), month++)
        {
            var day = Trading(first);
            if (day > today)
            {
                break;
            }

            Deposit(day, 450m);
            Buy(world, day, 200m, 5);
            if (first.Month is 1 or 4 or 7 or 10)
            {
                Buy(dividend, day, 450m, 7);
            }

            if (month % 4 == 1)
            {
                Buy(bonds, day, 400m, 9);
            }

            switch (month)
            {
                case 2:
                    Buy(nvidia, day.AddDays(2) <= today ? Trading(day.AddDays(2)) : day, 300m, 31);
                    break;
                case 4:
                    Buy(apple, Trading(day.AddDays(3)), 350m, 44);
                    break;
                case 5:
                    Buy(galp, Trading(day.AddDays(6)), 900m, 52);
                    break;
                case 8:
                    Buy(nvidia, Trading(day.AddDays(9)), 250m, 18);
                    break;
                case 14:
                    Buy(apple, Trading(day.AddDays(5)), 300m, 27);
                    break;
                case 10:
                    Sell(bonds, Trading(day.AddDays(8)), 0.2m);
                    break;
                case 12:
                    Sell(nvidia, Trading(day.AddDays(4)), 0.3m);
                    break;
                case 20:
                    if (Trading(day.AddDays(7)) <= today)
                    {
                        Sell(dividend, Trading(day.AddDays(7)), 0.15m);
                    }

                    break;
                case 22:
                    if (Trading(day.AddDays(3)) <= today)
                    {
                        Sell(apple, Trading(day.AddDays(3)), 0.2m);
                    }

                    break;
                case 17:
                    if (Trading(day.AddDays(11)) <= today)
                    {
                        Sell(galp, Trading(day.AddDays(11)), 0.25m);
                    }

                    break;
            }

            // Interest on the cash left, paid on the first of the month.
            if (cash > 0)
            {
                var interest = Math.Round(cash * 0.022m / 12m, 2);
                if (interest > 0)
                {
                    cash += interest;
                    batch.Cash.Add(new ImportedCash(CashKind.Interest, interest, When(first, 3, 0), "Interest on cash", DataSource.Demo));
                }
            }

            // Dividends of the month, on the shares held by then.
            foreach (var instrument in Catalogue.Where(static instrument => instrument.PerYear > 0))
            {
                if (!Pays(instrument, first.Month))
                {
                    continue;
                }

                var paid = Trading(first.AddDays(instrument.Currency == "USD" ? 13 : 20));
                if (paid > today || held[instrument.Isin] <= 0)
                {
                    continue;
                }

                var price = At(prices[instrument.Isin], paid);
                var perShare = Math.Round(price * (decimal)instrument.Yield / instrument.PerYear, 4);
                var gross = held[instrument.Isin] * perShare * Fx(instrument.Currency, paid);
                var withheld = instrument.Currency == "USD" ? Math.Round(gross * 0.15m, 2) : 0m;
                var amount = Math.Round(gross - withheld, 2);
                if (amount <= 0)
                {
                    continue;
                }

                cash += amount;
                batch.Dividends.Add(new ImportedDividend(instrument.Isin, When(paid, 11, 0), Math.Round(held[instrument.Isin], 6), perShare, amount, withheld > 0 ? withheld : null, "ORDINARY", DataSource.Demo));
            }
        }

        return new DemoPortfolio(batch, prices, usdEur, Math.Round(cash, 2), benchmarks, Sectors);
    }

    /// <summary>The sectors Yahoo Finance gives the demo shares.</summary>
    public static readonly IReadOnlyDictionary<string, (string Sector, string Industry)> Sectors = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["PTGAL0AM0009"] = ("Energy", "Oil & Gas Integrated"),
        ["US67066G1040"] = ("Technology", "Semiconductors"),
        ["US0378331005"] = ("Technology", "Consumer Electronics"),
    };

    private static bool Pays(DemoInstrument instrument, int month) => instrument.PerYear switch
    {
        4 when instrument.Currency == "USD" => month is 2 or 5 or 8 or 11,
        4 => month is 3 or 6 or 9 or 12,
        2 when instrument.Class == AssetClass.BondEtf => month is 1 or 7,
        2 => month is 5 or 9,
        _ => false,
    };

    private static SortedList<DateOnly, decimal> Walk(IReadOnlyList<DateOnly> days, double end, double drift, double volatility, Random random)
    {
        var values = new double[days.Count];
        values[^1] = end;
        for (var i = days.Count - 1; i > 0; i--)
        {
            var z = Normal(random);
            values[i - 1] = values[i] / Math.Exp(drift / 252 + volatility * z);
        }

        var series = new SortedList<DateOnly, decimal>();
        for (var i = 0; i < days.Count; i++)
        {
            series[days[i]] = Math.Round((decimal)values[i], 4);
        }

        return series;
    }

    private static double Normal(Random random)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static decimal At(SortedList<DateOnly, decimal> series, DateOnly day)
    {
        var value = series.Values[0];
        foreach (var (date, price) in series)
        {
            if (date > day)
            {
                break;
            }

            value = price;
        }

        return value;
    }

    private sealed record DemoInstrument(
        string Isin, string Ticker, string Name, string Currency, AssetClass Class, double Price, double Drift, double Volatility, double Yield, int PerYear);
}

/// <summary>The demo portfolio.</summary>
/// <param name="Batch">Instruments, trades, dividends and cash.</param>
/// <param name="Prices">Daily prices, by ISIN.</param>
/// <param name="UsdEur">Daily USD → EUR rates.</param>
/// <param name="Cash">The cash left today.</param>
/// <param name="Benchmarks">Daily closes of the benchmark indexes, by key.</param>
/// <param name="Sectors">The sector and industry of the shares, by ISIN.</param>
internal sealed record DemoPortfolio(
    ImportBatch Batch,
    IReadOnlyDictionary<string, SortedList<DateOnly, decimal>> Prices,
    SortedList<DateOnly, decimal> UsdEur,
    decimal Cash,
    IReadOnlyDictionary<string, SortedList<DateOnly, decimal>> Benchmarks,
    IReadOnlyDictionary<string, (string Sector, string Industry)> Sectors);
