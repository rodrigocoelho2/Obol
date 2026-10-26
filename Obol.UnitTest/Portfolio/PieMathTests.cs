using Obol.Library.Enums;
using Obol.WebApi.Features.Instruments.Models.Entities;
using Obol.WebApi.Features.Portfolio.Models.Entities;
using Obol.WebApi.Features.Portfolio.Services;

namespace Obol.UnitTest.Portfolio;

/// <summary>
/// The Overview's pies: the shares each pie owns valued with today's position, its cost from Trading 212, and what is
/// left of a position outside the pies.
/// </summary>
public sealed class PieMathTests
{
    private static readonly InstrumentRow World = TestSupport.Instrument(1, "IWDA");
    private static readonly InstrumentRow Apple = TestSupport.Instrument(2, "AAPL", "USD", AssetClass.Stock);
    private static readonly InstrumentRow Tesla = TestSupport.Instrument(3, "TSLA", "USD", AssetClass.Stock);

    private static Position Held(InstrumentRow instrument, decimal quantity, decimal value, decimal cost) =>
        new() { Instrument = instrument, Quantity = quantity, Value = value, Cost = cost };

    private static PortfolioData Data(IEnumerable<PieRow> pies, IEnumerable<PieHoldingRow> holdings) =>
        TestSupport.Data([World, Apple, Tesla], pies: pies, pieHoldings: holdings);

    [Fact]
    public void APieOwnsItsSharesAndTheRestIsOutside()
    {
        var positions = new List<Position> { Held(World, 10m, 1000m, 800m), Held(Apple, 4m, 800m, 600m), Held(Tesla, 2m, 600m, 700m) };
        var data = Data(
            [new PieRow { PieID = 7, Name = "Core" }],
            [
                new PieHoldingRow { PieID = 7, InstrumentID = 1, Quantity = 8m, Invested = 600m, Value = 790m },
                new PieHoldingRow { PieID = 7, InstrumentID = 2, Quantity = 4m, Invested = 600m, Value = 790m },
            ]);

        var (pies, outside) = PieMath.Group(data, positions);

        var pie = Assert.Single(pies);
        Assert.Equal("Core", pie.Pie.Name);
        Assert.Equal(2, pie.Holdings.Count);
        // Valued with today's position (8 of 10 shares of €1,000), cost as Trading 212 says.
        var world = pie.Holdings.Single(slice => slice.Instrument.InstrumentID == 1);
        Assert.Equal(800m, world.Value);
        Assert.Equal(600m, world.Invested);
        Assert.Equal(1600m, pie.Value);
        Assert.Equal(1200m, pie.Invested);
        Assert.Equal(400m, pie.Gain);
        Assert.Equal(1m / 3m, pie.GainPct!.Value, 6);

        // Tesla is all outside; two shares of the world ETF are left, with the rest of its cost.
        Assert.Equal(2, outside.Count);
        Assert.Equal(3, outside[0].Instrument.InstrumentID);
        var rest = outside.Single(slice => slice.Instrument.InstrumentID == 1);
        Assert.Equal(2m, rest.Quantity);
        Assert.Equal(200m, rest.Value);
        Assert.Equal(200m, rest.Invested);
        Assert.Equal(positions.Sum(static position => position.Value), pies.Sum(static group => group.Value) + outside.Sum(static slice => slice.Value));
    }

    [Fact]
    public void SharesSoldSinceThePieWasReadAreLeftOut()
    {
        var positions = new List<Position> { Held(World, 3m, 300m, 240m) };
        var data = Data(
            [new PieRow { PieID = 1, Name = "Tech" }, new PieRow { PieID = 2, Name = "World" }],
            [
                new PieHoldingRow { PieID = 1, InstrumentID = 2, Quantity = 4m, Invested = 600m },
                new PieHoldingRow { PieID = 2, InstrumentID = 1, Quantity = 5m, Invested = 400m },
            ]);

        var (pies, outside) = PieMath.Group(data, positions);

        // The Apple pie owns nothing the account still holds; the World pie can't own more than the 3 shares held.
        var pie = Assert.Single(pies);
        Assert.Equal("World", pie.Pie.Name);
        var slice = Assert.Single(pie.Holdings);
        Assert.Equal(3m, slice.Quantity);
        Assert.Equal(300m, slice.Value);
        Assert.Equal(240m, slice.Invested);
        Assert.Empty(outside);
    }

    [Fact]
    public void DustLeftByFractionalSharesStaysInThePie()
    {
        var positions = new List<Position> { Held(Apple, 1.0000001m, 200m, 150m) };
        var data = Data([new PieRow { PieID = 3, Name = "One" }], [new PieHoldingRow { PieID = 3, InstrumentID = 2, Quantity = 1m, Invested = 150m }]);

        var (pies, outside) = PieMath.Group(data, positions);

        Assert.Single(pies);
        Assert.Empty(outside);
    }

    [Fact]
    public void WithoutPiesEverythingIsOutside()
    {
        var positions = new List<Position> { Held(World, 10m, 1000m, 800m), Held(Apple, 4m, 800m, 600m) };

        var (pies, outside) = PieMath.Group(Data([], []), positions);

        Assert.Empty(pies);
        Assert.Equal(new[] { 1, 2 }, outside.Select(static slice => slice.Instrument.InstrumentID));
        Assert.Equal(800m, outside[0].Invested);
    }
}
