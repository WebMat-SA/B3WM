using B3WM.Services.Core;
using B3WM.Shared.Models;

namespace B3WM.Tests
{
    public class StructureEngineTests
    {
        [Fact]
        public void FirstBar_InitializesBorders()
        {
            var engine = new StructureEngine();
            var bar = Bar(high: 120, low: 100, close: 110);

            var step = engine.Step(bar, 15);

            Assert.True(step.Initialized);
            Assert.False(step.UpChanged);
            Assert.False(step.DownChanged);
            Assert.NotNull(engine.State);
            Assert.Equal(120, engine.State!.UpBorder);
            Assert.Equal(100, engine.State.DownBorder);
            Assert.Equal(120, engine.State.UpAuxBorder);
            Assert.Equal(100, engine.State.DownAuxBorder);
        }

        [Fact]
        public void UpperBreak_UpdatesTopAndFlags()
        {
            var engine = new StructureEngine();
            engine.Step(Bar(high: 120, low: 100, close: 110), 15);

            var step = engine.Step(Bar(high: 130, low: 105, close: 110), 15);

            Assert.True(step.UpChanged);
            Assert.False(step.DownChanged);
            Assert.Equal(130, engine.State!.UpBorder);
            Assert.Equal(105, engine.State.DownAuxBorder);
            Assert.False(engine.State.ExpectBuyDrop);
            Assert.True(engine.State.ExpectSellDrop);
        }

        [Fact]
        public void LowerBreak_UpdatesBottomAndFlags()
        {
            var engine = new StructureEngine();
            engine.Step(Bar(high: 120, low: 100, close: 110), 15);
            engine.Step(Bar(high: 130, low: 105, close: 110), 15);

            var step = engine.Step(Bar(high: 112, low: 90, close: 105), 15);

            Assert.False(step.UpChanged);
            Assert.True(step.DownChanged);
            Assert.Equal(90, engine.State!.DownBorder);
            Assert.Equal(112, engine.State.UpAuxBorder);
            Assert.True(engine.State.ExpectBuyDrop);
            Assert.False(engine.State.ExpectSellDrop);
        }

        [Fact]
        public void BothConditions_DoNotRunOnSameBar()
        {
            var engine = new StructureEngine();
            engine.Step(Bar(high: 100, low: 90, close: 95), 5);

            var step = engine.Step(Bar(high: 120, low: 80, close: 100), 5);

            Assert.True(step.UpChanged);
            Assert.False(step.DownChanged);
        }

        [Fact]
        public void Engine_MatchesLegacyGenerate_OnRandomSeries()
        {
            var random = new Random(12345);
            var engine = new StructureEngine();
            var legacy = new LegacyStructure();
            var close = 100.0;

            for (var i = 0; i < 500; i++)
            {
                close += random.Next(-15, 16);
                var high = close + random.Next(0, 12);
                var low = close - random.Next(0, 12);
                var bar = Bar(high, low, close);
                var range = random.Next(5, 31);

                var step = engine.Step(bar, range);
                legacy.Step(bar, range);

                Assert.Equal(legacy.UpBorder, engine.State!.UpBorder, 6);
                Assert.Equal(legacy.DownBorder, engine.State.DownBorder, 6);
                Assert.Equal(legacy.UpAuxBorder, engine.State.UpAuxBorder, 6);
                Assert.Equal(legacy.DownAuxBorder, engine.State.DownAuxBorder, 6);
                Assert.Equal(legacy.ExpectBuyDrop, engine.State.ExpectBuyDrop);
                Assert.Equal(legacy.ExpectSellDrop, engine.State.ExpectSellDrop);
                Assert.Equal(legacy.UpChanged, step.UpChanged);
                Assert.Equal(legacy.DownChanged, step.DownChanged);
            }
        }

        private static BarStorageItem Bar(double high, double low, double close) => new()
        {
            Symbol = "WINFUT",
            TimeFrame = 2,
            Open = close,
            High = high,
            Low = low,
            Close = close,
        };

        /// <summary>Cópia fiel da regra antiga de StructureService.Generate.</summary>
        private sealed class LegacyStructure
        {
            public double UpBorder { get; private set; }
            public double DownBorder { get; private set; }
            public double UpAuxBorder { get; private set; }
            public double DownAuxBorder { get; private set; }
            public bool ExpectBuyDrop { get; private set; } = true;
            public bool ExpectSellDrop { get; private set; } = true;
            public bool UpChanged { get; private set; }
            public bool DownChanged { get; private set; }

            private bool _initialized;

            public void Step(BarStorageItem bar, double range)
            {
                if (!_initialized)
                {
                    UpBorder = UpAuxBorder = bar.High;
                    DownBorder = DownAuxBorder = bar.Low;
                    _initialized = true;
                    UpChanged = DownChanged = false;
                    return;
                }

                UpChanged = DownChanged = false;
                var virtualUp = Math.Max(UpAuxBorder, bar.High);
                var virtualDown = Math.Min(DownAuxBorder, bar.Low);
                var isSizeChanger = false;

                if (virtualUp - bar.Close >= range && ExpectBuyDrop)
                {
                    UpAuxBorder = virtualUp;
                    UpBorder = UpAuxBorder;
                    ExpectSellDrop = true;
                    ExpectBuyDrop = false;
                    DownAuxBorder = bar.Low;
                    isSizeChanger = true;
                    UpChanged = true;
                }

                if (bar.Close - virtualDown >= range && ExpectSellDrop && !isSizeChanger)
                {
                    DownAuxBorder = virtualDown;
                    DownBorder = DownAuxBorder;
                    ExpectSellDrop = false;
                    ExpectBuyDrop = true;
                    UpAuxBorder = bar.High;
                    DownChanged = true;
                }

                UpAuxBorder = Math.Max(UpAuxBorder, bar.High);
                DownAuxBorder = Math.Min(DownAuxBorder, bar.Low);
            }
        }
    }
}
