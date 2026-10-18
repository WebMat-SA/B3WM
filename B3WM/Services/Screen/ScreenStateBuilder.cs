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
        /// <summary>
        /// Resolve o escopo por estrutura (automode) no backend e adiciona as
        /// camadas diárias (candles 1440 + volume do escopo). Usado pela JEV:
        /// todas as camadas são recortadas ao escopo [from,to] e o diário vai
        /// sempre. false = comportamento legado (tela / debug com corte fixo).
        /// </summary>
        public bool ResolveScope { get; set; }
    }

    /// <summary>
    /// Monta o MarketSnapshot WYSIWYG sobre o IMarketData, lendo os filtros do
    /// spec cru. Camada desligada/ausente entra como null (vira "desligado").
    /// Com <see cref="ScreenStateRequest.ResolveScope"/> resolve o escopo por
    /// estrutura (intraday no TF da estratégia; diário automode 1440 ou manual)
    /// e recorta todas as camadas, sem teto de tamanho.
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
            // Dados crus de apoio para a auditoria (não viram layer do state).
            var snapRaw = new Dictionary<string, object?>();
            if (spec.ValueKind != JsonValueKind.Undefined)
                snapRaw["spec"] = spec.Clone();
            var daily = ScreenSpec.Daily(spec);
            var tf = Defaults.TimeFrames.Contains(req.TimeFrame) ? req.TimeFrame : 5;
            var tfs = req.VisibleTimeFrames.Where(t => t > 0).Distinct().ToList();
            if (tfs.Count == 0) tfs.Add(tf);
            var thrBase = ScreenSpec.Int(spec, "thresholdBubble", Defaults.GetThresholdBubble(req.Symbol));
            var minDist = ScreenSpec.Num(spec, "structureRangeUpd", Defaults.GetMinDistance(req.Symbol));

            // Candles: escopo = dia inteiro recortado; legado = takeLast fixo.
            var allCandles = await _market.CandlesAsync(req.Symbol, tf, req.DisplayDate,
                req.ResolveScope ? int.MaxValue : req.LookbackCandles, ct);

            DateWindow? intraScope = null;
            var candles = allCandles;
            if (req.ResolveScope && allCandles.Count > 0)
            {
                if (ScreenSpec.Bool(spec, "profileAutoByPriceStructure", false))
                {
                    var structsTf = await _market.StructuresAsync(req.Symbol, tf, minDist, req.DisplayDate, ct);
                    var win = StructureWindowResolver.ResolveIntraday(
                        allCandles, structsTf, req.Symbol, tf, Defaults.GetTickSize(req.Symbol));
                    if (win is { } w)
                    {
                        candles = allCandles.Skip(w.StartIndex).Take(w.EndIndex - w.StartIndex).ToList();
                        intraScope = new DateWindow { From = w.From, To = w.To };
                    }
                }
                else if (req.ProfileFrom != null || req.ProfileTo != null)
                {
                    var f = req.ProfileFrom?.Date;
                    var t = req.ProfileTo?.Date;
                    var filtered = allCandles
                        .Where(c => (f == null || c.Date >= f) && (t == null || c.Date <= t))
                        .ToList();
                    if (filtered.Count > 0)
                    {
                        candles = filtered;
                        intraScope = new DateWindow { From = filtered[0].Date, To = filtered[^1].Date };
                    }
                }
                else
                {
                    intraScope = new DateWindow { From = candles[0].Date, To = candles[^1].Date };
                }
                intraScope ??= new DateWindow { From = candles[0].Date, To = candles[^1].Date };
            }

            List<BubbleStorageItem> bubbles = new();
            int totalDay = 0, visibleDay = 0;
            if (ScreenSpec.Bool(spec, "bubbleVisible", true))
            {
                var pool = await _market.BubblesAsync(req.Symbol, req.DisplayDate, ct);
                totalDay = pool.Count;
                var filtered = JevPrompt.ApplyBubbleFilter(pool, spec, thrBase).OrderBy(b => b.Date).ToList();
                visibleDay = filtered.Count;
                // Escopo: todas as bolhas do período (após filtro de tela);
                // legado: corte por LookbackBubbles.
                bubbles = req.ResolveScope && intraScope is { } sc
                    ? filtered.Where(b => b.Date >= sc.From && b.Date <= sc.To).ToList()
                    : filtered.TakeLast(req.LookbackBubbles).ToList();
                // Auditoria: o que chegou (pool cru) e o que passou no filtro
                // de tela (antes do corte). Não entra no state.
                snapRaw["bubblesPool"] = pool;
                snapRaw["bubblesFiltered"] = filtered;
            }

            VolumeLevelStorageItem? volume = null;
            if (ScreenSpec.Bool(spec, "profileVisible", true))
            {
                if (req.ResolveScope && intraScope is { } sc)
                {
                    var levels = await _market.VolumeLevelsAsync(req.Symbol, req.DisplayDate, sc.From, sc.To, ct);
                    volume = new VolumeLevelStorageItem
                    {
                        Symbol = req.Symbol, Date = req.DisplayDate, Volumes = levels,
                    };
                }
                else if (req.ProfileFrom == null && req.ProfileTo == null)
                {
                    volume = await _market.VolumeAsync(req.Symbol, req.DisplayDate, ct);
                }
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
            foreach (var kv in snapRaw) snap.Raw[kv.Key] = kv.Value;
            snap.Layers[SnapshotLayers.Candles] = candles;
            snap.Layers[SnapshotLayers.Bubbles] = bubbles;
            snap.Layers[SnapshotLayers.Volume] = volume;
            snap.Layers["lastPrice"] = lastPrice;
            if (intraScope != null) snap.Layers[SnapshotLayers.ScopeIntraday] = intraScope;
            snap.Layers[SnapshotLayers.Header] = new HeaderView
            {
                DisplayDate = req.DisplayDate,
                Mode = ScreenSpec.Str(spec, "dateRangeMode", "intraday"),
                LastPrice = lastPrice,
                VisibleTimeFrames = tfs,
                MinDistance = minDist,
                ThresholdBubble = thrBase,
                VwapVisible = ScreenSpec.Bool(spec, "vwapVisible", true),
                PanelOpen = req.IncludeDaily,
                TradingHistoryVisible = ScreenSpec.Bool(spec, "tradingHistoryVisible", true),
                PositionVisible = ScreenSpec.Bool(spec, "positionVisible", true),
                OpenOrdersVisible = ScreenSpec.Bool(spec, "openOrdersVisible", true),
                BubblesTotalDay = totalDay,
                BubblesVisibleDay = visibleDay,
            };

            // Estruturas intraday (TFs visíveis) + estruturas diárias (1440).
            List<StructureStorageItem> dailyStructs = new();
            if (req.ResolveScope)
            {
                var dailyMinDist = ScreenSpec.Num(daily, "structureRangeUpd", Defaults.GetMinDistanceDaily(req.Symbol));
                dailyStructs = await _market.DailyStructuresAsync(req.Symbol, dailyMinDist, req.DisplayDate, 365, ct);
            }

            if (ScreenSpec.Bool(spec, "structureVisible", true) || req.ResolveScope)
            {
                var all = new List<StructureStorageItem>();
                foreach (var t in tfs)
                    all.AddRange(await _market.StructuresAsync(req.Symbol, t, minDist, req.DisplayDate, ct));
                if (req.ResolveScope)
                {
                    all.AddRange(dailyStructs);
                }
                else if (req.IncludeDaily && ScreenSpec.Bool(daily, "structureVisible", true))
                {
                    all.AddRange(await _market.DailyStructuresAsync(req.Symbol,
                        ScreenSpec.Num(daily, "structureRangeUpd", Defaults.GetMinDistanceDaily(req.Symbol)),
                        req.DisplayDate, ct: ct));
                }
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
            {
                snap.Layers[SnapshotLayers.Vwap] =
                    await _market.VwapAsync(req.Symbol, req.DisplayDate, ct);
            }

            // --- Bloco diário (candles 1440 + volume do escopo) ---
            if (req.ResolveScope)
            {
                var displayDay = req.DisplayDate.Date;
                var dailyBarsAll = await _market.DailyCandlesAsync(req.Symbol, displayDay.AddDays(-365), displayDay, ct);
                DateTime dFrom, dTo;
                if (ScreenSpec.Bool(daily, "profileAutoByPriceStructure", true))
                {
                    (dFrom, dTo) = StructureWindowResolver.ResolveDaily(
                        dailyBarsAll, dailyStructs, req.Symbol, displayDay);
                }
                else
                {
                    dTo = (req.DailyTo?.Date ?? displayDay).Date;
                    dFrom = (req.DailyFrom?.Date ?? dTo.AddDays(-60)).Date;
                }
                var dailyScope = new DateWindow { From = dFrom, To = dTo };
                var dailyCandles = dailyBarsAll
                    .Where(b => b.Date.Date >= dFrom.Date && b.Date.Date <= dTo.Date)
                    .ToList();
                snap.Layers[SnapshotLayers.ScopeDaily] = dailyScope;
                snap.Layers[SnapshotLayers.DailyCandles] = dailyCandles;
                snap.Layers[SnapshotLayers.DailyVolume] = new VolumeLevelStorageItem
                {
                    Symbol = req.Symbol, Date = displayDay,
                    Volumes = await _market.VolumeLevelsAsync(req.Symbol, displayDay, dFrom, dTo, ct),
                };
                snap.Layers[SnapshotLayers.DailyWindow] = dailyScope;

                if (ScreenSpec.Bool(daily, "extremeVisible", true))
                    snap.Layers[SnapshotLayers.ExtremesDaily] =
                        await _market.DailyExtremesAsync(req.Symbol, dFrom, dTo,
                            ScreenSpec.Num(daily, "extremeNoiseSensitivity", Defaults.Extreme.NoiseSensitivity),
                            ScreenSpec.Num(daily, "extremeMinimumProminence", Defaults.Extreme.MinimumProminence), ct);
                if (ScreenSpec.Bool(daily, "pivotVisible", true))
                    snap.Layers[SnapshotLayers.PivotDaily] = await _market.DailyPivotAsync(
                        req.Symbol, PivotService.ClampLineCount(ScreenSpec.Int(daily, "pivotLineCount", 2)),
                        req.DisplayDate, ct);
            }
            else if (req.IncludeDaily)
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
