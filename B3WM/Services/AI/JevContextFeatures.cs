using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Parâmetros das features de contexto da Jev. Todos os cortes são em
    /// ticks para funcionar igual em WINFUT/WDOFUT. Os defaults são o ponto
    /// de partida; o estudo offline (Data/JEV) calibra os valores finais.
    /// </summary>
    public sealed record JevFeatureOptions(
        int ZoneNearTicks = 50,
        double LowVolumeRank = 0.30,
        double StrongAtr = 2.0,
        double StrongMinTicks = 20,
        int MoveLookback = 20,
        int ZoneMaxItems = 8,
        double ZoneMaxTicks = 100,
        double VolumeWindowTicks = 10,
        int HvnCount = 5,
        int LvnCount = 5,
        double DailyZoneMinConfidence = 0.50)
    {
        public static JevFeatureOptions Default => new();

        /// <summary>Perfil amplo: core (zona diária + bolhas filtradas + movimento).</summary>
        public static JevFeatureOptions Broad => new(
            ZoneNearTicks: 100,
            LowVolumeRank: 0.20,
            StrongAtr: 1.5,
            StrongMinTicks: 20,
            MoveLookback: 30,
            ZoneMaxItems: 8,
            ZoneMaxTicks: 120);

        /// <summary>Perfil estrito: zona curta + baixo volume/absorção.</summary>
        public static JevFeatureOptions Strict => Broad with
        {
            ZoneNearTicks = 30,
            MoveLookback = 10,
        };
    }

    public sealed record JevVolumeNode(
        double Price, long Total, double Share, double DistanceTicks = 0);

    public sealed record JevVolumeSummary(
        double Poc,
        double Vah,
        double Val,
        long DeltaTotal,
        long Total,
        double PriceRank,
        bool PriceInLowVolume,
        JevVolumeNode? NearestLvn,
        IReadOnlyList<JevVolumeNode> Hvn,
        IReadOnlyList<JevVolumeNode> Lvn);

    public sealed record JevRecentMove(
        string Direction,
        int Candles,
        double ChangeTicks,
        double RangeTicks,
        double AtrTicks,
        double Efficiency,
        bool Strong);

    public sealed record JevBubbleSummary(
        int Count,
        int BuyCount,
        int SellCount,
        decimal BuyAmount,
        decimal SellAmount,
        decimal NetAmount)
    {
        public static readonly JevBubbleSummary Empty = new(0, 0, 0, 0, 0, 0);
    }

    public sealed record JevZoneFeature(
        string Kind,
        string Source,
        string Level,
        double Price,
        double DistanceTicks,
        string Side,
        DateTime? TouchedAt,
        bool Reclaimed,
        double? VolumeRank,
        bool LowVolume,
        JevBubbleSummary FilteredBubbles,
        JevBubbleSummary ClusterBubbles,
        string Absorption,
        double? ZoneLow,
        double? ZoneHigh,
        double? ZoneStrength,
        double? ZoneConfidence,
        bool IsMajor);

    public sealed record JevConfluence(
        bool AtDailyZone,
        string DailyZoneType,
        bool OppositeDailyZone,
        bool LowVolumeNearValley,
        bool FilteredBubblesAtZone,
        string AbsorptionSide,
        string PriorMove,
        string PriorMoveDirection,
        bool SetupReversaoPerdaForca,
        double? OppositeDailyZonePrice = null,
        double? OppositeDailyZoneDistanceTicks = null,
        bool OppositeDailyZoneInRange = false,
        double? LowVolumeValleyRank = null,
        double? LowVolumeValleyDistanceTicks = null,
        int FilteredBubbleCount = 0,
        bool PriorMoveStrong = false);

    public sealed record JevContextFeatures(
        double Price,
        double TickSize,
        JevRecentMove RecentMove,
        JevVolumeSummary? DailyVolume,
        JevVolumeSummary? IntradayVolume,
        IReadOnlyList<JevZoneFeature> Zones,
        JevConfluence Confluence);

    /// <summary>
    /// Extração pura das features que respondem à regra do usuário:
    /// topo/fundo diário + bolhas filtradas no nível + baixo volume perto de
    /// vale intraday + movimento prévio forte = reversão/perda de força.
    /// Sem I/O; o estudo offline e a montagem do state usam os mesmos métodos.
    /// </summary>
    public static class JevContextAnalyzer
    {
        public static JevContextFeatures Build(
            string symbol,
            int timeFrame,
            IReadOnlyList<BarStorageItem> candles,
            VolumeLevelStorageItem? intradayVolume,
            VolumeLevelStorageItem? dailyVolume,
            PivotStorageItem? pivIntra,
            PivotStorageItem? pivDaily,
            ExtremeStorageItem? extIntra,
            ExtremeStorageItem? extDaily,
            IEnumerable<BubbleStorageItem> filteredBubbles,
            IEnumerable<BubbleStorageItem> clusterBubbles,
            DateTime asOf,
            JevFeatureOptions? options = null)
        {
            var o = options ?? JevFeatureOptions.Default;
            var tickSize = Defaults.GetTickSize(symbol);
            if (candles.Count == 0 || tickSize <= 0)
            {
                return new JevContextFeatures(
                    0, tickSize,
                    new JevRecentMove("lateral", 0, 0, 0, 0, 0, false),
                    null, null,
                    Array.Empty<JevZoneFeature>(),
                    new JevConfluence(false, "", false, false, false, "none",
                        "lateral", "lateral", false));
            }

            var price = candles[^1].Close;
            var intraday = SummarizeVolume(intradayVolume, price, tickSize, o);
            var daily = SummarizeVolume(dailyVolume, price, tickSize, o);
            var zones = BuildZones(candles, pivIntra, pivDaily, extIntra, extDaily,
                intradayVolume, dailyVolume, filteredBubbles, clusterBubbles,
                asOf, tickSize, o);
            var move = BuildRecentMove(candles, tickSize, o);
            var confluence = EvaluateConfluence(zones, move, o, price);
            return new JevContextFeatures(
                price, tickSize, move, daily, intraday, zones, confluence);
        }

        /// <summary>
        /// POC, value area 70%, delta, HVN/LVN e o rank de volume do preço
        /// (janela em ticks). Devolve null quando não há perfil.
        /// </summary>
        public static JevVolumeSummary? SummarizeVolume(
            VolumeLevelStorageItem? item,
            double price,
            double tickSize,
            JevFeatureOptions options)
        {
            var levels = item?.Volumes?
                .Where(v => v.Total > 0)
                .OrderBy(v => v.Price)
                .ToList();
            if (levels == null || levels.Count == 0 || tickSize <= 0) return null;

            var total = levels.Sum(v => v.Total);
            if (total <= 0) return null;

            var pocIdx = 0;
            for (var i = 1; i < levels.Count; i++)
                if (levels[i].Total > levels[pocIdx].Total) pocIdx = i;

            var (vahIdx, valIdx) = ValueArea(levels, pocIdx, total);
            var windows = BuildWindows(levels, tickSize, options.VolumeWindowTicks);
            var rank = RankAt(windows, price);
            var low = rank is { } r && r <= options.LowVolumeRank;

            var hvn = levels
                .OrderByDescending(v => v.Total)
                .Take(options.HvnCount)
                .Select(v => Node(v, total))
                .ToList();
            var lvn = levels
                .OrderBy(v => v.Total)
                .Take(options.LvnCount)
                .Select(v => Node(v, total))
                .ToList();

            JevVolumeNode? nearestLvn = null;
            var candidates = Enumerable.Range(0, levels.Count)
                .Where(i => Math.Abs(levels[i].Price - price) <= options.ZoneMaxTicks * tickSize)
                .ToList();
            if (candidates.Count == 0)
                candidates = Enumerable.Range(0, levels.Count).ToList();
            if (candidates.Count > 0)
            {
                var bestIdx = candidates
                    .OrderBy(i => windows.Sums[i])
                    .ThenBy(i => Math.Abs(levels[i].Price - price))
                    .First();
                nearestLvn = new JevVolumeNode(
                    levels[bestIdx].Price,
                    levels[bestIdx].Total,
                    (double)levels[bestIdx].Total / total,
                    Math.Abs(levels[bestIdx].Price - price) / tickSize);
            }

            return new JevVolumeSummary(
                levels[pocIdx].Price,
                levels[vahIdx].Price,
                levels[valIdx].Price,
                levels.Sum(v => v.Delta),
                total,
                rank ?? 0,
                low,
                nearestLvn,
                hvn,
                lvn);
        }

        /// <summary>
        /// Movimento recente em ticks. A janela é o último <see cref="JevFeatureOptions.MoveLookback"/>
        /// candles; a direção é o sinal do close final contra o open inicial.
        /// </summary>
        public static JevRecentMove BuildRecentMove(
            IReadOnlyList<BarStorageItem> candles,
            double tickSize,
            JevFeatureOptions options)
        {
            if (candles.Count == 0 || tickSize <= 0)
                return new JevRecentMove("lateral", 0, 0, 0, 0, 0, false);

            var count = Math.Min(Math.Max(1, options.MoveLookback), candles.Count);
            var window = candles.Skip(candles.Count - count).ToList();
            var first = window[0];
            var last = window[^1];
            var change = last.Close - first.Open;

            double high = double.MinValue, low = double.MaxValue;
            var trs = new double[window.Count];
            for (var i = 0; i < window.Count; i++)
            {
                high = Math.Max(high, window[i].High);
                low = Math.Min(low, window[i].Low);
                trs[i] = i == 0
                    ? window[i].High - window[i].Low
                    : Math.Max(window[i].High - window[i].Low,
                        Math.Max(Math.Abs(window[i].High - window[i - 1].Close),
                            Math.Abs(window[i].Low - window[i - 1].Close)));
            }

            var atr = trs.Average();
            var sumAbs = 0.0;
            for (var i = 1; i < window.Count; i++)
                sumAbs += Math.Abs(window[i].Close - window[i - 1].Close);
            if (window.Count == 1)
                sumAbs = Math.Abs(window[0].Close - window[0].Open);

            var efficiency = sumAbs > 0
                ? Math.Clamp(Math.Abs(change) / sumAbs, 0, 1)
                : 0;
            var direction = change > 0 ? "alta" : change < 0 ? "baixa" : "lateral";
            var move = new JevRecentMove(
                direction,
                window.Count,
                change / tickSize,
                (high - low) / tickSize,
                atr / tickSize,
                efficiency,
                false);
            return move with { Strong = IsStrongMove(move, options) };
        }

        public static bool IsStrongMove(JevRecentMove move, JevFeatureOptions options)
        {
            var abs = Math.Abs(move.ChangeTicks);
            if (abs <= 0) return false;
            return (move.AtrTicks > 0 && abs >= options.StrongAtr * move.AtrTicks)
                || abs >= options.StrongMinTicks;
        }

        /// <summary>
        /// Zonas relevantes (pivôs + topos/vales intraday/diários) com volume
        /// no nível e resumo de bolhas filtradas (filtro real da tela) versus
        /// cluster (tudo que foi enviado).
        /// </summary>
        public static IReadOnlyList<JevZoneFeature> BuildZones(
            IReadOnlyList<BarStorageItem> candles,
            PivotStorageItem? pivIntra,
            PivotStorageItem? pivDaily,
            ExtremeStorageItem? extIntra,
            ExtremeStorageItem? extDaily,
            VolumeLevelStorageItem? intradayVolume,
            VolumeLevelStorageItem? dailyVolume,
            IEnumerable<BubbleStorageItem> filteredBubbles,
            IEnumerable<BubbleStorageItem> clusterBubbles,
            DateTime asOf,
            double tickSize,
            JevFeatureOptions options)
        {
            if (candles.Count == 0 || tickSize <= 0)
                return Array.Empty<JevZoneFeature>();

            var levels = JevPrompt.BuildLevelCandidates(
                candles, pivIntra, pivDaily, extIntra, extDaily, tickSize);
            var filtered = filteredBubbles as IReadOnlyList<BubbleStorageItem>
                ?? filteredBubbles.ToList();
            var cluster = clusterBubbles as IReadOnlyList<BubbleStorageItem>
                ?? clusterBubbles.ToList();

            var from = asOf.AddMinutes(-JevPrompt.LevelClusterMinutes);
            var radius = JevPrompt.LevelClusterTicks * tickSize;
            var intradayWindows = BuildWindows(intradayVolume, tickSize, options.VolumeWindowTicks);
            var dailyWindows = BuildWindows(dailyVolume, tickSize, options.VolumeWindowTicks);

            var output = new List<JevZoneFeature>();
            foreach (var level in levels
                         .Where(l => l.DistanceTicks <= options.ZoneMaxTicks ||
                             (l.TouchedAt is { } touched && touched >= from))
                         .OrderBy(l => l.DistanceTicks)
                         .Take(options.ZoneMaxItems))
            {
                var useDaily = level.Source == "diario" || level.Source == "W-1";
                var windows = useDaily ? dailyWindows : intradayWindows;
                var rank = RankAt(windows, level.Value);
                var low = rank is { } r && r <= options.LowVolumeRank;
                var filteredNear = SummarizeBubbles(
                    filtered, level.Value, from, asOf, radius);
                var clusterNear = SummarizeBubbles(
                    cluster, level.Value, from, asOf, radius);
                var (zoneLow, zoneHigh, strength, confidence, major) =
                    ZoneMeta(level, level.Source == "diario" ? extDaily : extIntra,
                        options.DailyZoneMinConfidence);
                output.Add(new JevZoneFeature(
                    level.Kind, level.Source, level.Level, level.Value,
                    level.DistanceTicks, level.Side, level.TouchedAt, level.Reclaimed,
                    rank, low, filteredNear, clusterNear,
                    Absorption(level, filteredNear),
                    zoneLow, zoneHigh, strength, confidence, major));
            }
            return output;
        }

        /// <summary>
        /// Aplica os thresholds de calibração sobre as zonas já extraídas.
        /// O setup exige movimento forte, zona diária oposta ao movimento,
        /// bolhas filtradas no nível e (baixo volume perto de vale OU absorção).
        /// </summary>
        public static JevConfluence EvaluateConfluence(
            IReadOnlyList<JevZoneFeature> zones,
            JevRecentMove move,
            JevFeatureOptions options,
            double? price = null)
        {
            var priorDir = move.Direction;
            var strong = IsStrongMove(move, options);
            var oppositeType = priorDir == "alta" ? "topo"
                : priorDir == "baixa" ? "vale" : "";

            var daily = zones
                .Where(z => z.Source == "diario" &&
                    (z.Kind == "topo" || z.Kind == "vale"))
                .ToList();
            var nearDaily = daily
                .Where(z => z.DistanceTicks <= options.ZoneNearTicks)
                .OrderBy(z => z.DistanceTicks)
                .ToList();
            var atDaily = nearDaily.Count > 0;
            var dailyType = nearDaily.FirstOrDefault()?.Kind ?? "";
            var oppositeDaily = nearDaily.FirstOrDefault(z => z.Kind == oppositeType);

            var valleys = zones
                .Where(z => z.Source == "intra" && z.Kind == "vale")
                .OrderBy(z => z.DistanceTicks)
                .ToList();
            var lowVolumeValley = valleys.FirstOrDefault(z =>
                z.DistanceTicks <= options.ZoneNearTicks &&
                z.VolumeRank is { } rank && rank <= options.LowVolumeRank);

            var relevant = oppositeDaily
                ?? nearDaily.FirstOrDefault()
                ?? valleys.FirstOrDefault();
            var filteredAtZone = relevant?.FilteredBubbles.Count > 0;
            var absorption = relevant?.Absorption ?? "none";

            var setup = strong
                && priorDir != "lateral"
                && oppositeDaily != null
                && filteredAtZone
                && (lowVolumeValley != null || absorption != "none");

            var inRange = price is { } p
                && oppositeDaily?.ZoneLow is { } low
                && oppositeDaily.ZoneHigh is { } high
                && p >= low && p <= high;

            return new JevConfluence(
                atDaily,
                dailyType,
                oppositeDaily != null,
                lowVolumeValley != null,
                filteredAtZone,
                absorption,
                MoveLabel(move, strong),
                priorDir,
                setup,
                oppositeDaily?.Price,
                oppositeDaily?.DistanceTicks,
                inRange,
                lowVolumeValley?.VolumeRank,
                lowVolumeValley?.DistanceTicks,
                relevant?.FilteredBubbles.Count ?? 0,
                strong);
        }

        /// <summary>
        /// Setup amplo (core): movimento forte + zona diária oposta + bolhas
        /// filtradas no nível. É o sinal com maior recall no estudo offline.
        /// </summary>
        public static bool IsSetupContexto(JevConfluence c) =>
            c.PriorMoveStrong && c.OppositeDailyZone && c.FilteredBubblesAtZone;

        /// <summary>
        /// Setup estrito: core + baixo volume perto de vale ou absorção, com
        /// zona curta. É o sinal de maior precisão no estudo offline.
        /// </summary>
        public static bool IsSetupConfirmacao(JevConfluence c) =>
            IsSetupContexto(c) &&
            (c.LowVolumeNearValley || c.AbsorptionSide != "none");

        private static string MoveLabel(JevRecentMove move, bool strong) =>
            move.Direction == "lateral"
                ? "lateral"
                : $"{move.Direction}_{(strong ? "forte" : "fraca")}";

        private static (int Vah, int Val) ValueArea(
            IReadOnlyList<VolumeLevel> levels, int pocIdx, long total)
        {
            double accumulated = levels[pocIdx].Total;
            var left = pocIdx - 1;
            var right = pocIdx + 1;
            var vahIdx = pocIdx;
            var valIdx = pocIdx;
            var target = total * 0.70;

            while (accumulated < target && (left >= 0 || right < levels.Count))
            {
                var takeLeft = right >= levels.Count ||
                    (left >= 0 && levels[left].Total >= levels[right].Total);
                var idx = takeLeft ? left-- : right++;
                accumulated += levels[idx].Total;
                if (levels[idx].Price > levels[vahIdx].Price) vahIdx = idx;
                if (levels[idx].Price < levels[valIdx].Price) valIdx = idx;
            }
            return (vahIdx, valIdx);
        }

        private static JevVolumeNode Node(VolumeLevel level, long total) => new(
            level.Price,
            level.Total,
            total > 0 ? (double)level.Total / total : 0);

        private static (double? Low, double? High, double? Strength,
            double? Confidence, bool Major) ZoneMeta(
            JevPrompt.JevLevelCandidate candidate, ExtremeStorageItem? extremes,
            double minConfidence)
        {
            if (extremes == null) return (null, null, null, null, false);
            var extreme = extremes.Extremes
                .Where(e => e.Type is ExtremeType.Top or ExtremeType.Valley)
                .FirstOrDefault(e => Math.Abs(e.Position - candidate.Value) < 1e-6);
            if (extreme == null) return (null, null, null, null, false);
            return (extreme.StartPosition, extreme.EndPosition,
                extreme.Strength, extreme.Confidence,
                extreme.Confidence >= minConfidence);
        }

        private static JevBubbleSummary SummarizeBubbles(
            IReadOnlyList<BubbleStorageItem> bubbles,
            double price,
            DateTime from,
            DateTime to,
            double radius)
        {
            var near = bubbles
                .Where(b => b.Date >= from && b.Date <= to &&
                    Math.Abs(b.Price - price) <= radius)
                .ToList();
            if (near.Count == 0) return JevBubbleSummary.Empty;

            var buys = near.Where(b => b.ActionType == Ticks2.ActionType.Buy).ToList();
            var sells = near.Where(b => b.ActionType == Ticks2.ActionType.Sale).ToList();
            var buyAmount = buys.Sum(b => b.Amount);
            var sellAmount = sells.Sum(b => b.Amount);
            return new JevBubbleSummary(
                near.Count, buys.Count, sells.Count,
                buyAmount, sellAmount, buyAmount - sellAmount);
        }

        private static string Absorption(
            JevPrompt.JevLevelCandidate candidate, JevBubbleSummary bubbles)
        {
            if (bubbles.Count == 0) return "none";
            var resistance = candidate.Side == "resistance" || candidate.Kind == "topo";
            var support = candidate.Side == "support" || candidate.Kind == "vale";
            if (resistance && bubbles.SellAmount > bubbles.BuyAmount) return "sell";
            if (support && bubbles.BuyAmount > bubbles.SellAmount) return "buy";
            return "none";
        }

        private sealed record VolumeWindows(double[] Prices, double[] Sums);

        private static VolumeWindows? BuildWindows(
            VolumeLevelStorageItem? item, double tickSize, double windowTicks)
        {
            var levels = item?.Volumes?
                .Where(v => v.Total > 0)
                .OrderBy(v => v.Price)
                .ToList();
            if (levels == null || levels.Count == 0 || tickSize <= 0) return null;
            return BuildWindows(levels, tickSize, windowTicks);
        }

        private static VolumeWindows BuildWindows(
            IReadOnlyList<VolumeLevel> levels, double tickSize, double windowTicks)
        {
            var n = levels.Count;
            var prices = new double[n];
            var prefix = new double[n + 1];
            for (var i = 0; i < n; i++)
            {
                prices[i] = levels[i].Price;
                prefix[i + 1] = prefix[i] + levels[i].Total;
            }

            var sums = new double[n];
            var window = Math.Max(0, windowTicks) * tickSize;
            var lo = 0;
            var hi = 0;
            for (var i = 0; i < n; i++)
            {
                while (hi < n && prices[hi] <= prices[i] + window + 1e-9) hi++;
                while (lo < n && prices[lo] < prices[i] - window - 1e-9) lo++;
                sums[i] = prefix[hi] - prefix[lo];
            }
            return new VolumeWindows(prices, sums);
        }

        private static double? RankAt(VolumeWindows? windows, double price)
        {
            if (windows == null || windows.Prices.Length == 0) return null;
            var idx = NearestIndex(windows.Prices, price);
            if (idx < 0) return null;
            var target = windows.Sums[idx];
            var count = 0;
            foreach (var sum in windows.Sums)
                if (sum <= target + 1e-9) count++;
            return (double)count / windows.Sums.Length;
        }

        private static int NearestIndex(IReadOnlyList<double> prices, double price)
        {
            if (prices.Count == 0) return -1;
            var lo = 0;
            var hi = prices.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (prices[mid] < price) lo = mid + 1;
                else if (prices[mid] > price) hi = mid - 1;
                else return mid;
            }
            if (lo >= prices.Count) return prices.Count - 1;
            if (hi < 0) return 0;
            return Math.Abs(prices[lo] - price) <= Math.Abs(prices[hi] - price)
                ? lo : hi;
        }
    }
}
