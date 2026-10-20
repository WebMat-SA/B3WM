using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Services.Market;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Services.Screen
{
    /// <summary>Pedido de montagem do estado da tela (spec cru + janela).</summary>
    public sealed class ScreenStateRequest
    {
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; } = 5;
        public DateTime DisplayDate { get; set; } = DateTime.Today;
        /// <summary>
        /// Corte histórico do snapshot. Quando informado, só entram barras
        /// fechadas até este instante; usado no replay de um gatilho antigo.
        /// null = comportamento ao vivo/dia inteiro.
        /// </summary>
        public DateTime? AsOf { get; set; }
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
                req.ResolveScope || req.AsOf != null ? int.MaxValue : req.LookbackCandles, ct);
            if (req.AsOf is { } asOf)
                allCandles = allCandles.Where(c => c.Date.AddMinutes(tf) <= asOf).ToList();

            DateWindow? intraScope = null;
            var candles = allCandles;
            if (req.ResolveScope && allCandles.Count > 0)
            {
                if (ScreenSpec.Bool(spec, "profileAutoByPriceStructure", false))
                {
                    var structsTf = await _market.StructuresAsync(req.Symbol, tf, minDist, req.DisplayDate, ct);
                    if (req.AsOf is not null)
                    {
                        var lastBar = allCandles[^1].Date;
                        structsTf = structsTf.Where(s => s.Date <= lastBar).ToList();
                    }
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
                var poolAsOf = req.AsOf is { } asOfBubbles
                    ? pool.Where(b => b.Date <= asOfBubbles).ToList()
                    : pool;
                totalDay = poolAsOf.Count;
                var filtered = JevPrompt.ApplyBubbleFilter(poolAsOf, spec, thrBase).OrderBy(b => b.Date).ToList();
                visibleDay = filtered.Count;
                // Escopo: todas as bolhas do período (após filtro de tela);
                // no replay, inclui a bolha do gatilho até AsOf.
                bubbles = req.ResolveScope && intraScope is { } sc
                    ? filtered.Where(b => b.Date >= sc.From && b.Date <= (req.AsOf ?? sc.To)).ToList()
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
                if (req.AsOf is { } asOfDaily && asOfDaily.Date == req.DisplayDate.Date)
                    dailyStructs = dailyStructs.Where(s => s.Date.Date < asOfDaily.Date).ToList();
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
                if (req.AsOf is { } asOfStructs)
                {
                    var lastIntra = candles.Count > 0 ? candles[^1].Date : asOfStructs;
                    all = all.Where(s => s.TimeFrame == 1440
                        ? s.Date.Date < asOfStructs.Date
                        : s.Date <= lastIntra).ToList();
                }
                snap.Layers[SnapshotLayers.Structures] = all.OrderBy(s => s.Date).ToList();
                snap.Layers[SnapshotLayers.StructureAuxVisibleFlag] =
                    ScreenSpec.Bool(spec, "structureAuxVisible", true);
            }

            if (ScreenSpec.Bool(spec, "extremeVisible", true))
                snap.Layers[SnapshotLayers.ExtremesIntra] =
                    (req.ResolveScope
                        ? BuildScopedExtremes(req.Symbol, volume, intraScope, spec)
                        : null)
                    ?? await _market.IntradayExtremesAsync(req.Symbol, ct);

            if (ScreenSpec.Bool(spec, "pivotVisible", true))
                snap.Layers[SnapshotLayers.PivotIntra] = await _market.IntradayPivotAsync(
                    req.Symbol, PivotService.ClampLineCount(ScreenSpec.Int(spec, "pivotLineCount", 2)),
                    req.DisplayDate, ct);

            if (ScreenSpec.Bool(spec, "vwapVisible", true) && lastPrice != null)
            {
                var scopedVwap = candles.LastOrDefault(c => c.VwapPrice.HasValue)?.VwapPrice;
                snap.Layers[SnapshotLayers.Vwap] =
                    scopedVwap ?? await _market.VwapAsync(req.Symbol, req.DisplayDate, ct);
            }

            // --- Bloco diário (candles 1440 + volume do escopo) ---
            if (req.ResolveScope)
            {
                var displayDay = req.DisplayDate.Date;
                var dailyBarsAll = await _market.DailyCandlesAsync(req.Symbol, displayDay.AddDays(-365), displayDay, ct);
                var dailyBars = dailyBarsAll;
                if (req.AsOf is { } asOfDaily && asOfDaily.Date == displayDay)
                {
                    dailyBars = dailyBarsAll.Where(b => b.Date.Date < displayDay).ToList();
                    var partial = await BuildPartialDailyBarAsync(req.Symbol, displayDay, asOfDaily, ct);
                    if (partial != null) dailyBars.Add(partial);
                    dailyBars = dailyBars.OrderBy(b => b.Date).ToList();
                }
                DateTime dFrom, dTo;
                if (ScreenSpec.Bool(daily, "profileAutoByPriceStructure", true))
                {
                    (dFrom, dTo) = StructureWindowResolver.ResolveDaily(
                        dailyBars, dailyStructs, req.Symbol, displayDay);
                }
                else
                {
                    dTo = (req.DailyTo?.Date ?? displayDay).Date;
                    dFrom = (req.DailyFrom?.Date ?? dTo.AddDays(-60)).Date;
                }
                var dailyScope = new DateWindow { From = dFrom, To = dTo };
                var dailyCandles = dailyBars
                    .Where(b => b.Date.Date >= dFrom.Date && b.Date.Date <= dTo.Date)
                    .ToList();
                var dailyVolumeTo = req.AsOf is { } asOfVolume && dTo.Date == asOfVolume.Date
                    ? asOfVolume
                    : dTo;
                snap.Layers[SnapshotLayers.ScopeDaily] = dailyScope;
                snap.Layers[SnapshotLayers.DailyCandles] = dailyCandles;
                snap.Layers[SnapshotLayers.DailyVolume] = new VolumeLevelStorageItem
                {
                    Symbol = req.Symbol, Date = displayDay,
                    Volumes = await _market.VolumeLevelsAsync(req.Symbol, displayDay, dFrom, dailyVolumeTo, ct),
                };
                snap.Layers[SnapshotLayers.DailyWindow] = dailyScope;

                if (ScreenSpec.Bool(daily, "extremeVisible", true))
                    snap.Layers[SnapshotLayers.ExtremesDaily] =
                        await _market.DailyExtremesAsync(req.Symbol, dFrom, dTo,
                            ScreenSpec.Num(daily, "extremeNoiseSensitivity", Defaults.Extreme.NoiseSensitivity),
                            ScreenSpec.Num(daily, "extremeMinimumProminence", Defaults.Extreme.MinimumProminence),
                            req.AsOf, ct);
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
                            ScreenSpec.Num(daily, "extremeMinimumProminence", Defaults.Extreme.MinimumProminence),
                            req.AsOf, ct);
                    snap.Layers[SnapshotLayers.DailyWindow] = new DateWindow { From = from, To = to };
                }
                if (ScreenSpec.Bool(daily, "pivotVisible", true))
                    snap.Layers[SnapshotLayers.PivotDaily] = await _market.DailyPivotAsync(
                        req.Symbol, PivotService.ClampLineCount(ScreenSpec.Int(daily, "pivotLineCount", 2)),
                        req.DisplayDate, ct);
            }

            return snap;
        }

        /// <summary>
        /// Candle diário parcial do dia do replay, agregado das barras 1MIN
        /// fechadas até <paramref name="asOf"/>. Evita usar o fechamento
        /// final do dia como se já fosse conhecido no gatilho.
        /// </summary>
        private async Task<BarStorageItem?> BuildPartialDailyBarAsync(
            string symbol, DateTime day, DateTime asOf, CancellationToken ct)
        {
            var bars = await _market.CandlesAsync(symbol, 1, day, int.MaxValue, ct);
            var closed = bars
                .Where(b => b.Date.AddMinutes(1) <= asOf)
                .OrderBy(b => b.Date)
                .ToList();
            if (closed.Count == 0) return null;

            return new BarStorageItem
            {
                Date = day.Date,
                Symbol = symbol,
                TimeFrame = 1440,
                Open = closed[0].Open,
                High = closed.Max(b => b.High),
                Low = closed.Min(b => b.Low),
                Close = closed[^1].Close,
                Volume = closed.Sum(b => b.Volume),
                VwapPrice = closed.LastOrDefault(b => b.VwapPrice.HasValue)?.VwapPrice,
            };
        }

        /// <summary>
        /// Topos/vales intraday recalculados sobre o perfil de volume já
        /// recortado ao escopo (mesmo pipeline do automode). Sem perfil
        /// visível, devolve null para o chamador usar o snapshot ao vivo.
        /// </summary>
        private static ExtremeStorageItem? BuildScopedExtremes(
            string symbol,
            VolumeLevelStorageItem? volume,
            DateWindow? scope,
            JsonElement spec)
        {
            if (volume?.Volumes == null) return null;

            var levels = volume.Volumes.OrderBy(v => v.Price).ToList();
            var options = new ExtremeDetectorOptions
            {
                NoiseSensitivity = ScreenSpec.Num(spec, "extremeNoiseSensitivity",
                    Defaults.Extreme.NoiseSensitivity),
                MinimumProminence = ScreenSpec.Num(spec, "extremeMinimumProminence",
                    Defaults.Extreme.MinimumProminence),
            };
            var result = ExtremeDetector.Detect(
                levels.Select(v => v.Price).ToArray(),
                levels.Select(v => (double)v.Total).ToArray(),
                options);

            return new ExtremeStorageItem
            {
                Date = DateTime.Now,
                Symbol = symbol,
                PeriodFrom = scope?.From,
                PeriodTo = scope?.To,
                Config = options,
                Extremes = result.Extremes,
                Structures = result.Structures,
                Statistics = result.Statistics,
            };
        }
    }
}
