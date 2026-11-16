using B3WM.Services.Research;

namespace B3WM.Tests
{
    public class StructureResearchReportBuilderTests
    {
        [Fact]
        public void Build_ComputesChangesFalseBreaksAndDivergence()
        {
            var date = new DateTime(2026, 11, 16);
            var manual = new List<StructureResearchPoint>
            {
                Point(date.AddMinutes(0), StructureResearchVariants.Manual, 120, 100, 110, 115, 105, 100),
                Point(date.AddMinutes(2), StructureResearchVariants.Manual, 130, 100, 115, 131, 110, 100, upChanged: true),
                Point(date.AddMinutes(4), StructureResearchVariants.Manual, 130, 100, 118, 122, 112, 100),
                Point(date.AddMinutes(6), StructureResearchVariants.Manual, 130, 100, 125, 128, 118, 100),
                Point(date.AddMinutes(8), StructureResearchVariants.Manual, 130, 100, 126, 129, 120, 100),
            };

            var atr = new List<StructureResearchPoint>
            {
                Point(date.AddMinutes(0), StructureResearchVariants.AtrPerBar, 120, 100, 110, 115, 105, 50, 10),
                Point(date.AddMinutes(2), StructureResearchVariants.AtrPerBar, 120, 90, 95, 100, 89, 50, 10, downChanged: true),
                Point(date.AddMinutes(4), StructureResearchVariants.AtrPerBar, 120, 90, 105, 110, 95, 150, 10),
                Point(date.AddMinutes(6), StructureResearchVariants.AtrPerBar, 130, 90, 115, 131, 110, 150, 10, upChanged: true),
                Point(date.AddMinutes(8), StructureResearchVariants.AtrPerBar, 130, 90, 110, 115, 105, 50, 10),
            };

            var report = StructureResearchReportBuilder.Build(
                "WINFUT", 2, date,
                new Dictionary<string, IReadOnlyList<StructureResearchPoint>>
                {
                    [StructureResearchVariants.Manual] = manual,
                    [StructureResearchVariants.AtrPerBar] = atr,
                },
                falseBreakBarsShort: 10,
                falseBreakBarsLong: 20,
                atrPeriod: 14,
                atrMultiplier: 1.5,
                manualRange: 100);

            Assert.Equal(2, report.Variants.Count);

            var manualReport = report.Variants.Single(v => v.Variant == StructureResearchVariants.Manual);
            Assert.Equal(1, manualReport.UpChanges);
            Assert.Equal(0, manualReport.DownChanges);
            Assert.Equal(1, manualReport.TotalChanges);
            Assert.Equal(1, manualReport.FalseBreaksShort);
            Assert.Equal(0, manualReport.ChangesNotMatchedManual);
            Assert.Equal(2, manualReport.AvgLegBars, 6);
            Assert.Equal(2, manualReport.MedianLegBars, 6);

            var atrReport = report.Variants.Single(v => v.Variant == StructureResearchVariants.AtrPerBar);
            Assert.Equal(1, atrReport.UpChanges);
            Assert.Equal(1, atrReport.DownChanges);
            Assert.Equal(2, atrReport.TotalChanges);
            Assert.Equal(2, atrReport.FalseBreaksShort);
            Assert.Equal(2, atrReport.FalseBreaksLong);
            Assert.Equal(1, atrReport.FalseBreakRateShort, 6);
            Assert.Equal(2, atrReport.ChangesNotMatchedManual);
            Assert.Equal(50, atrReport.RangeMin);
            Assert.Equal(90, atrReport.RangeAvg, 6);
            Assert.Equal(150, atrReport.RangeMax);
            Assert.Equal(10, atrReport.AtrAvg, 6);
        }

        private static StructureResearchPoint Point(
            DateTime date,
            string variant,
            double up,
            double down,
            double close,
            double high,
            double low,
            double range,
            double atr = 10,
            bool upChanged = false,
            bool downChanged = false) => new()
        {
            Date = date,
            Symbol = "WINFUT",
            TimeFrame = 2,
            Variant = variant,
            Open = close,
            High = high,
            Low = low,
            Close = close,
            UpBorder = up,
            DownBorder = down,
            UpAuxBorder = up,
            DownAuxBorder = down,
            RangeUsed = range,
            Atr = atr,
            AtrTicks = atr / 5,
            UpChanged = upChanged,
            DownChanged = downChanged,
        };
    }
}
