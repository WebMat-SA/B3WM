using B3WM.Services.Core;
using B3WM.Shared.Models;

namespace B3WM.Tests
{
    public class SymbolRegistryTests
    {
        [Fact]
        public void All_ContainsFourCanonicalSymbols()
        {
            Assert.Equal(
                new[] { "WINFUT", "INDFUT", "WDOFUT", "DOLFUT" },
                SymbolRegistry.Names);
            Assert.All(SymbolRegistry.Names, s => Assert.True(SymbolRegistry.IsKnown(s)));
        }

        [Fact]
        public void Contracts_MapToMt5RootsAndFrequencies()
        {
            Assert.Equal("WIN", SymbolRegistry.Get("WINFUT").Mt5Root);
            Assert.Equal("IND", SymbolRegistry.Get("INDFUT").Mt5Root);
            Assert.Equal("WDO", SymbolRegistry.Get("WDOFUT").Mt5Root);
            Assert.Equal("DOL", SymbolRegistry.Get("DOLFUT").Mt5Root);

            Assert.Equal(ContractFrequency.Bimonthly, SymbolRegistry.Get("INDFUT").Frequency);
            Assert.Equal(ContractFrequency.Monthly, SymbolRegistry.Get("DOLFUT").Frequency);
        }

        [Theory]
        [InlineData("WINFUT", 5.0, 1.0, 250, 250.0, 1000.0)]
        [InlineData("INDFUT", 5.0, 1.0, 15, 250.0, 1000.0)]
        [InlineData("WDOFUT", 0.5, 10.0, 500, 2.5, 100.0)]
        [InlineData("DOLFUT", 0.5, 50.0, 15, 2.5, 100.0)]
        public void Defaults_DelegateToRegistry(
            string symbol,
            double tickSize,
            double pointValue,
            int threshold,
            double minDistance,
            double minDistanceDaily)
        {
            Assert.Equal(tickSize, Defaults.GetTickSize(symbol));
            Assert.Equal(pointValue, Defaults.GetPointValue(symbol));
            Assert.Equal(threshold, Defaults.GetThresholdBubble(symbol));
            Assert.Equal(minDistance, Defaults.GetMinDistance(symbol));
            Assert.Equal(minDistanceDaily, Defaults.GetMinDistanceDaily(symbol));
        }

        [Fact]
        public void LegacySymbols_KeepHistoricalValues()
        {
            Assert.Equal(5.0, Defaults.GetTickSize(Defaults.Symbols.WINFUT));
            Assert.Equal(1.0, Defaults.GetPointValue(Defaults.Symbols.WINFUT));
            Assert.Equal(250, Defaults.GetThresholdBubble(Defaults.Symbols.WINFUT));
            Assert.Equal(250.0, Defaults.GetMinDistance(Defaults.Symbols.WINFUT));
            Assert.Equal(1000.0, Defaults.GetMinDistanceDaily(Defaults.Symbols.WINFUT));

            Assert.Equal(0.5, Defaults.GetTickSize(Defaults.Symbols.WDOFUT));
            Assert.Equal(10.0, Defaults.GetPointValue(Defaults.Symbols.WDOFUT));
            Assert.Equal(500, Defaults.GetThresholdBubble(Defaults.Symbols.WDOFUT));
            Assert.Equal(2.5, Defaults.GetMinDistance(Defaults.Symbols.WDOFUT));
            Assert.Equal(100.0, Defaults.GetMinDistanceDaily(Defaults.Symbols.WDOFUT));
        }

        [Theory]
        [InlineData("PETR4")]
        [InlineData("")]
        [InlineData(null)]
        public void UnknownSymbol_KeepsLegacyFallback(string? symbol)
        {
            Assert.False(SymbolRegistry.IsKnown(symbol));
            Assert.Equal(1.0, Defaults.GetTickSize(symbol!));
            Assert.Equal(1.0, Defaults.GetPointValue(symbol!));
            Assert.Equal(250, Defaults.GetThresholdBubble(symbol!));
            Assert.Equal(250.0, Defaults.GetMinDistance(symbol!));
            Assert.Equal(1000.0, Defaults.GetMinDistanceDaily(symbol!));
        }

        [Fact]
        public void IsKnown_IsCaseInsensitive()
        {
            Assert.True(SymbolRegistry.IsKnown("indfut"));
            Assert.True(SymbolRegistry.IsKnown(" DOLFUT "));
        }

        [Theory]
        [InlineData("WINFUT", 1.5, 0.3)]
        [InlineData("INDFUT", 1.5, 0.3)]
        [InlineData("WDOFUT", 1.0, 1.0)]
        [InlineData("DOLFUT", 1.0, 1.0)]
        public void AtrMultipliers_ResolvePerSymbol(
            string symbol, double intraday, double daily)
        {
            var options = new StructureAtrOptions();

            Assert.Equal(intraday, options.MultiplierFor(symbol));
            Assert.Equal(daily, options.DailyMultiplierFor(symbol));
        }
    }
}
