using Obol.Library.Enums;
using Obol.WebApi.Features.Instruments.Services;

namespace Obol.UnitTest.Instruments;

/// <summary>
/// Instrument keys, symbols and classes: what makes one instrument the same whether it came from the API or a CSV.
/// </summary>
public sealed class InstrumentNamesTests
{
    [Theory]
    [InlineData("VUSAl_EQ", "VUSA")]
    [InlineData("AAPL_US_EQ", "AAPL")]
    [InlineData("BRK.B_US_EQ", "BRK.B")]
    [InlineData("IWDAa_EQ", "IWDA")]
    [InlineData("GALP_PT_EQ", "GALP")]
    [InlineData("SAPd_EQ", "SAP")]
    [InlineData("IWDA", "IWDA")]
    [InlineData(" nvda ", "NVDA")]
    [InlineData(null, "")]
    public void SymbolsDropTheExchangeAndTheMarket(string? ticker, string expected) =>
        Assert.Equal(expected, InstrumentNames.Symbol(ticker));

    [Fact]
    public void TheIsinIsTheKeyWhenThereIsOne()
    {
        Assert.Equal("IE00B4L5Y983", InstrumentNames.Key(" ie00b4l5y983 ", "IWDAa_EQ"));
        Assert.Equal("T212:IWDAa_EQ", InstrumentNames.Key("not-an-isin", "IWDAa_EQ"));
        Assert.Equal("T212:UNKNOWN", InstrumentNames.Key(null, null));
        Assert.True(InstrumentNames.Key(null, new string('X', 80)).Length <= 40);
    }

    [Theory]
    [InlineData("US0378331005", true)]
    [InlineData("PTGAL0AM0009", true)]
    [InlineData("US037833100", false)]
    [InlineData("1S0378331005", false)]
    [InlineData("", false)]
    public void OnlyRealIsinsAreKept(string isin, bool valid) =>
        Assert.Equal(valid, InstrumentNames.CleanIsin(isin) is not null);

    [Theory]
    [InlineData("STOCK", "Apple", AssetClass.Stock)]
    [InlineData("ETF", "iShares Core MSCI World UCITS ETF (Acc)", AssetClass.EquityEtf)]
    [InlineData("ETF", "iShares Core € Govt Bond UCITS ETF (Dist)", AssetClass.BondEtf)]
    [InlineData("ETF", "iShares Physical Gold ETC", AssetClass.Commodity)]
    [InlineData("CRYPTO", "Bitcoin", AssetClass.Crypto)]
    [InlineData("WARRANT", "Something", AssetClass.Other)]
    [InlineData(null, "Vanguard FTSE All-World UCITS ETF (USD) Distributing", AssetClass.EquityEtf)]
    [InlineData(null, "iShares Core Global Aggregate Bond UCITS ETF", AssetClass.BondEtf)]
    [InlineData(null, "Xtrackers II EUR Overnight Rate Swap UCITS ETF", AssetClass.BondEtf)]
    [InlineData(null, "Galp Energia SGPS", AssetClass.Stock)]
    [InlineData(null, "Accenture", AssetClass.Stock)]
    [InlineData(null, "Distribution Solutions Group", AssetClass.Stock)]
    public void ClassesComeFromTheTypeOrTheName(string? type, string name, AssetClass expected) =>
        Assert.Equal(expected, InstrumentNames.Classify(type, name));

    [Fact]
    public void TruncateTrimsAndCuts()
    {
        Assert.Equal("abc", InstrumentNames.Truncate("  abc  ", 10));
        Assert.Equal("ab", InstrumentNames.Truncate("abc", 2));
        Assert.Equal(string.Empty, InstrumentNames.Truncate(null, 2));
    }
}
