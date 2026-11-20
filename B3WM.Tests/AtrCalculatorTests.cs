using B3WM.Services.Core;
using B3WM.Shared.Models;

namespace B3WM.Tests
{
    public class AtrCalculatorTests
    {
        [Fact]
        public void TrueRange_UsesPreviousClose_OnGap()
        {
            var previous = Bar(100, 105, 95, 100);
            var current = Bar(120, 130, 118, 125);

            var tr = AtrCalculator.TrueRange(current, previous);

            Assert.Equal(30, tr, 6);
        }

        [Fact]
        public void AtrState_AveragesLastPeriod()
        {
            var atr = new AtrState(3);

            atr.Add(Bar(100, 110, 100, 105)); // TR = 10
            atr.Add(Bar(105, 125, 105, 120)); // TR = 20
            atr.Add(Bar(120, 150, 120, 145)); // TR = 30

            Assert.Equal(20, atr.Value, 6);
            Assert.Equal(3, atr.Samples);

            atr.Add(Bar(145, 185, 145, 180)); // TR = 40; janela 20/30/40

            Assert.Equal(30, atr.Value, 6);
            Assert.Equal(6, atr.ValueInTicks(5), 6);
        }

        [Fact]
        public void AtrState_ReturnsZero_BeforeFirstBar()
        {
            var atr = new AtrState(14);

            Assert.Equal(0, atr.Value);
            Assert.Equal(0, atr.Samples);
        }

        [Fact]
        public void ClampRangePoints_RespectsFloorAndCap()
        {
            // ATR 200 * 1.5 = 300 pontos.
            Assert.Equal(300, AtrCalculator.ClampRangePoints(200, 1.5, 5, 5, 400), 6);

            // Piso de 5 ticks (25 pontos) acima do range calculado (15).
            Assert.Equal(25, AtrCalculator.ClampRangePoints(10, 1.5, 5, 5, 400), 6);

            // Teto de 400 ticks (2000 pontos).
            Assert.Equal(2000, AtrCalculator.ClampRangePoints(2000, 1.5, 5, 5, 400), 6);

            Assert.Equal(0, AtrCalculator.ClampRangePoints(0, 1.5, 5, 5, 400), 6);
        }

        private static BarStorageItem Bar(double open, double high, double low, double close) => new()
        {
            Symbol = "WINFUT",
            TimeFrame = 2,
            Open = open,
            High = high,
            Low = low,
            Close = close,
        };
    }
}
