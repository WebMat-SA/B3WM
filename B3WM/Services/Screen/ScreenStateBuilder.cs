using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Services.Market;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;

namespace B3WM.Services.Screen
{
    /// <summary>Pedido de montagem do estado da tela (spec cru + janela).</summary>
    public sealed class ScreenStateRequest
    {
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; } = 5;
        public DateTime DisplayDate { get; set; } = DateTime.Today;
        public List<int> VisibleTimeFrames { get; set; } = new();
        /// <summary>Blob cru (SymbolConfig.toJson do app). {} = defaults.</summary>
        public JsonElement Spec { get; set; }
        public DateTime? ProfileFrom { get; set; }
        public DateTime? ProfileTo { get; set; }
        public DateTime? DailyFrom { get; set; }
        public DateTime? DailyTo { get; set; }
        public bool IncludeDaily { get; set; }
        public int LookbackCandles { get; set; } = 60;
        public int LookbackBubbles { get; set; } = 30;
    }

    /// <summary>
    /// Monta o MarketSnapshot WYSIWYG sobre o IMarketData, lendo os filtros
    /// do spec cru. Camada desligada/ausente entra como null (vira linha
    /// "desligado/sem dados" no texto). Sem espelho tipado.
    /// </summary>
    public sealed class ScreenStateBuilder
    {
        private readonly IMarketData _market;
        private readonly ILogger<ScreenStateBuilder> _logger;

        public ScreenStateBuilder(IMarketData market, ILogger<ScreenStateBuilder> logger)
        {
            _market = market;
            _logger = logger;
        }

        public async Task<MarketSnapshot> BuildAsync(ScreenStateRequest req, CancellationToken ct = default)
        {
            var spec = req.Spec;
            var daily = ScreenSpec.Daily(spec);
            var tf = Defaults.TimeFrames.Contains(req.TimeFrame) ? req.TimeFrame : 5;
            var tfs = req.VisibleTimeFrames.Where(t => t > 0).Distinct().ToList();
            if (tfs.Count == 0) tfs.Add(tf);
            var thrBase = ScreenSpec.Int(spec, "thresholdBubble", Defaults.GetThresholdBubble(req.Symbol));

            var candles = await _market.CandlesAsync(req.Symbol, tf,
                req.DisplayDate, req.LookbackCandles, ct);

            List<BubbleStorageItem> bubbles = new();
            int totalDay = 0, visibleDay = 0;
            if (ScreenSpec.Bool(spec, "bubbleVisible", true))
            {
                var pool = await _market.BubblesAsync(req.Symbol, req.DisplayDate, ct);
                totalDay = pool.Count;
                bubbles = JevPrompt.ApplyBubbleFilter(pool, spec, thrBase)
                    .OrderBy(b => b.Date).TakeLast(req.LookbackBubbles).ToList();
                visibleDay = JevPrompt.ApplyBubbleFilter(pool, spec, thrBase).Count;
            }

            VolumeLevelStorageItem? volume = null;
            if (ScreenSpec.Bool(spec, "profileVisible", true))
            {
                if (req.ProfileFrom == null && req.ProfileTo == null)
                    volume = await _market.VolumeAsync(req.Symbol, req.DisplayDate, ct);
                else
                {
                    var levels = await _market.VolumeLevelsAsync(
                        req.Symbol, req.DisplayDate, req.ProfileFrom, req.ProfileTo, ct);
                    volume = new VolumeLevelStorageItem
                    {
                        Symbol = req.Symbol, Date = req.DisplayDate, Volumes = levels,
                    };
                }
            }

            var lastPrice = candles.Count > 0 ? candles[^1].Close : (double?)null;
            var snap = new MarketSnapshot();
            snap.Layers[SnapshotLayers.Candles] = candles;
            snap.Layers[SnapshotLayers.Bubbles] = bubbles;
            snap.Layers[SnapshotLayers.Volume] = volume;
            snap.Layers["lastPrice"] = lastPrice;
            snap.Layers[SnapshotLayers.Header] = new HeaderView
            {
                DisplayDate = req.DisplayDate,
                Mode = ScreenSpec.Str(spec, "dateRangeMode", "intraday"),
                LastPrice = lastPrice,
                VisibleTimeFrames = tfs,
                MinDistance = ScreenSpec.Num(spec, "structureRangeUpd",
                    Defaults.GetMinDistance(req.Symbol)),
                ThresholdBubble = thrBase,
                VwapVisible = ScreenSpec.Bool(spec, "vwapVisible", true),
                PanelOpen = req.IncludeDaily,
                TradingHistoryVisible = ScreenSpec.Bool(spec, "tradingHistoryVisible", true),
                PositionVisible = ScreenSpec.Bool(spec, "positionVisible", true),
                OpenOrdersVisible = ScreenSpec.Bool(spec, "openOrdersVisible", true),
                BubblesTotalDay = totalDay,
                BubblesVisibleDay = visibleDay,
            };

            if (ScreenSpec.Bool(spec, "structureVisible", true))
            {
                var all = new List<StructureStorageItem>();
                var minDist = ScreenSpec.Num(spec, "structureRangeUpd",
                    Defaults.GetMinDistance(req.Symbol));
                foreach (var t in tfs)
                    all.AddRange(await _market.StructuresAsync(req.Symbol, t, minDist, req.DisplayDate, ct));
                if (req.IncludeDaily && ScreenSpec.Bool(daily, "structureVisible", true))
                    all.AddRange(await _market.DailyStructuresAsync(req.Symbol,
                        ScreenSpec.Num(daily, "structureRangeUpd",
                            Defaults.GetMinDistanceDaily(req.Symbol)),
                        req.DisplayDate, ct: ct));
                snap.Layers[SnapshotLayers.Structures] = all.OrderBy(s => s.Date).ToList();
                snap.Layers[SnapshotLayers.StructureAuxVisibleFlag] =
                    ScreenSpec.Bool(spec, "structureAuxVisible", true);
            }

            if (ScreenSpec.Bool(spec, "extremeVisible", true))
                snap.Layers[SnapshotLayers.ExtremesIntra] =
                    await _market.IntradayExtremesAsync(req.Symbol, ct);

            if (ScreenSpec.Bool(spec, "pivotVisible", true))
                snap.Layers[SnapshotLayers.PivotIntra] = await _market.IntradayPivotAsync(
                    req.Symbol, PivotService.ClampLineCount(ScreenSpec.Int(spec, "pivotLineCount", 2)),
                    req.DisplayDate, ct);

            if (ScreenSpec.Bool(spec, "vwapVisible", true) && lastPrice != null)
                snap.Layers[SnapshotLayers.Vwap] =
                    JevPrompt.ComputeVwap(candles, req.DisplayDate);

            if (req.IncludeDaily)
            {
                if (ScreenSpec.Bool(daily, "extremeVisible", true))
                {
                    var to = (req.DailyTo?.Date ?? req.DisplayDate).Date;
                    var from = (req.DailyFrom?.Date ?? to.AddDays(-60)).Date;
                    snap.Layers[SnapshotLayers.ExtremesDaily] =
                        await _market.DailyExtremesAsync(req.Symbol, from, to,
                            ScreenSpec.Num(daily, "extremeNoiseSensitivity", Defaults.Extreme.NoiseSensitivity),
                            ScreenSpec.Num(daily, "extremeMinimumProminence", Defaults.Extreme.MinimumProminence), ct);
                    snap.Layers[SnapshotLayers.DailyWindow] = new DateWindow { From = from, To = to };
                }
                if (ScreenSpec.Bool(daily, "pivotVisible", true))
                    snap.Layers[SnapshotLayers.PivotDaily] = await _market.DailyPivotAsync(
                        req.Symbol, PivotService.ClampLineCount(ScreenSpec.Int(daily, "pivotLineCount", 2)),
                        req.DisplayDate, ct);
            }

            return snap;
        }

    }
}
