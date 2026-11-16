namespace B3WM.Services.Research
{
    /// <summary>
    /// Métricas puras do A/B (issue #25). Recebe os pontos de cada variante e
    /// devolve um relatório comparável; sem I/O e sem depender do serviço.
    /// </summary>
    public static class StructureResearchReportBuilder
    {
        public static StructureResearchReport Build(
            string symbol,
            int timeFrame,
            DateTime date,
            IReadOnlyDictionary<string, IReadOnlyList<StructureResearchPoint>> pointsByVariant,
            int falseBreakBarsShort = 10,
            int falseBreakBarsLong = 20,
            int atrPeriod = 0,
            double atrMultiplier = 0,
            double manualRange = 0)
        {
            var report = new StructureResearchReport
            {
                Symbol = symbol,
                TimeFrame = timeFrame,
                Date = date.Date,
                GeneratedAt = DateTime.Now,
                AtrPeriod = atrPeriod,
                AtrMultiplier = atrMultiplier,
                ManualRange = manualRange,
            };

            var manual = GetPoints(pointsByVariant, StructureResearchVariants.Manual);
            var manualChanges = BuildChangeSet(manual);

            foreach (var variant in StructureResearchVariants.All)
            {
                var points = GetPoints(pointsByVariant, variant);
                if (points.Count == 0)
                    continue;

                report.Variants.Add(BuildVariant(
                    variant, points, manualChanges,
                    falseBreakBarsShort, falseBreakBarsLong));
            }

            return report;
        }

        private static IReadOnlyList<StructureResearchPoint> GetPoints(
            IReadOnlyDictionary<string, IReadOnlyList<StructureResearchPoint>> pointsByVariant,
            string variant) =>
            pointsByVariant.TryGetValue(variant, out var points) && points != null
                ? points
                : Array.Empty<StructureResearchPoint>();

        private static HashSet<(DateTime Date, bool Up)> BuildChangeSet(
            IReadOnlyList<StructureResearchPoint> points)
        {
            var set = new HashSet<(DateTime Date, bool Up)>();
            foreach (var point in points)
            {
                if (point.UpChanged) set.Add((point.Date, true));
                if (point.DownChanged) set.Add((point.Date, false));
            }
            return set;
        }

        private static StructureResearchVariantReport BuildVariant(
            string variant,
            IReadOnlyList<StructureResearchPoint> source,
            HashSet<(DateTime Date, bool Up)> manualChanges,
            int shortBars,
            int longBars)
        {
            var points = source.OrderBy(p => p.Date).ToList();
            var upChanges = points.Count(p => p.UpChanged);
            var downChanges = points.Count(p => p.DownChanged);
            var durations = BuildLegDurations(points);

            var falseShort = 0;
            var falseLong = 0;
            var changeCount = 0;
            var followThroughSum = 0.0;
            var followThroughCount = 0;

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (!point.UpChanged && !point.DownChanged)
                    continue;

                var oldUp = i > 0 ? points[i - 1].UpBorder : point.UpBorder;
                var oldDown = i > 0 ? points[i - 1].DownBorder : point.DownBorder;

                if (point.UpChanged)
                {
                    changeCount++;
                    if (IsFalseBreakUp(points, i, oldUp, shortBars)) falseShort++;
                    if (IsFalseBreakUp(points, i, oldUp, longBars)) falseLong++;
                    followThroughSum += FollowThroughUp(points, i, longBars);
                    followThroughCount++;
                }

                if (point.DownChanged)
                {
                    changeCount++;
                    if (IsFalseBreakDown(points, i, oldDown, shortBars)) falseShort++;
                    if (IsFalseBreakDown(points, i, oldDown, longBars)) falseLong++;
                    followThroughSum += FollowThroughDown(points, i, longBars);
                    followThroughCount++;
                }
            }

            var unmatched = 0;
            if (!variant.Equals(StructureResearchVariants.Manual, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var point in points)
                {
                    if (point.UpChanged && !manualChanges.Contains((point.Date, true)))
                        unmatched++;
                    if (point.DownChanged && !manualChanges.Contains((point.Date, false)))
                        unmatched++;
                }
            }

            return new StructureResearchVariantReport
            {
                Variant = variant,
                Bars = points.Count,
                UpChanges = upChanges,
                DownChanges = downChanges,
                TotalChanges = upChanges + downChanges,
                AvgLegBars = durations.Count > 0 ? durations.Average() : 0,
                MedianLegBars = Median(durations),
                RangeMin = points.Min(p => p.RangeUsed),
                RangeAvg = points.Average(p => p.RangeUsed),
                RangeMax = points.Max(p => p.RangeUsed),
                AtrMin = points.Min(p => p.Atr),
                AtrAvg = points.Average(p => p.Atr),
                AtrMax = points.Max(p => p.Atr),
                FalseBreaksShort = falseShort,
                FalseBreaksLong = falseLong,
                FalseBreakRateShort = changeCount > 0 ? (double)falseShort / changeCount : 0,
                FalseBreakRateLong = changeCount > 0 ? (double)falseLong / changeCount : 0,
                FollowThroughAtrAvg = followThroughCount > 0
                    ? followThroughSum / followThroughCount
                    : 0,
                ChangesNotMatchedManual = unmatched,
            };
        }

        private static List<int> BuildLegDurations(IReadOnlyList<StructureResearchPoint> points)
        {
            var durations = new List<int>();
            if (points.Count == 0)
                return durations;

            var lastChange = 0;
            for (var i = 0; i < points.Count; i++)
            {
                if (!points[i].UpChanged && !points[i].DownChanged)
                    continue;

                durations.Add(Math.Max(1, i - lastChange));
                lastChange = i;
            }

            durations.Add(Math.Max(1, points.Count - 1 - lastChange));
            return durations;
        }

        private static bool IsFalseBreakUp(
            IReadOnlyList<StructureResearchPoint> points,
            int index,
            double oldUpBorder,
            int horizon)
        {
            var end = Math.Min(points.Count - 1, index + Math.Max(1, horizon));
            for (var j = index + 1; j <= end; j++)
            {
                if (points[j].Close <= oldUpBorder)
                    return true;
            }
            return false;
        }

        private static bool IsFalseBreakDown(
            IReadOnlyList<StructureResearchPoint> points,
            int index,
            double oldDownBorder,
            int horizon)
        {
            var end = Math.Min(points.Count - 1, index + Math.Max(1, horizon));
            for (var j = index + 1; j <= end; j++)
            {
                if (points[j].Close >= oldDownBorder)
                    return true;
            }
            return false;
        }

        private static double FollowThroughUp(
            IReadOnlyList<StructureResearchPoint> points,
            int index,
            int horizon)
        {
            var atr = points[index].Atr;
            if (atr <= 0) return 0;

            var end = Math.Min(points.Count - 1, index + Math.Max(1, horizon));
            var maxHigh = points[index].High;
            for (var j = index + 1; j <= end; j++)
                maxHigh = Math.Max(maxHigh, points[j].High);

            return (maxHigh - points[index].UpBorder) / atr;
        }

        private static double FollowThroughDown(
            IReadOnlyList<StructureResearchPoint> points,
            int index,
            int horizon)
        {
            var atr = points[index].Atr;
            if (atr <= 0) return 0;

            var end = Math.Min(points.Count - 1, index + Math.Max(1, horizon));
            var minLow = points[index].Low;
            for (var j = index + 1; j <= end; j++)
                minLow = Math.Min(minLow, points[j].Low);

            return (points[index].DownBorder - minLow) / atr;
        }

        private static double Median(List<int> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();
            var mid = values.Count / 2;
            return values.Count % 2 == 1
                ? values[mid]
                : (values[mid - 1] + values[mid]) / 2.0;
        }
    }
}
