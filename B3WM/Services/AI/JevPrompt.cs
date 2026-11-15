using System.Text.Json;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Montagem do state JSON da Jev (contrato de prompt). Pertence à camada
    /// de análise (usado pela JevAnalysisStrategy e pelo endpoint avulso),
    /// NÃO ao transporte: o <see cref="JevService"/> só envia HTTP e devolve
    /// o JSON cru. Regra: nada aqui faz I/O.
    /// </summary>
    public static class JevPrompt
    {
        /// <summary>Raio (ticks) do cluster de bolhas ao redor de um nível.</summary>
        public const double LevelClusterTicks = 30;
        /// <summary>Janela (minutos) do cluster de bolhas ao redor de um nível.</summary>
        public const int LevelClusterMinutes = 120;
        /// <summary>Máximo de bolhas por nível no cluster.</summary>
        public const int LevelClusterMaxBubbles = 12;
        private const double LevelContextMaxTicks = 100;
        private const int LevelContextMaxItems = 8;
        private const int LevelReclaimCandles = 6;

        /// <summary>
        /// Predicado único do filtro de bolhas da tela (single source; o
        /// record de gatilho em Strategies delega para cá).
        /// </summary>
        public static bool BubblePasses(
            BubbleStorageItem b,
            int thresholdBubble,
            IReadOnlyDictionary<int, int> agentThresholds,
            IReadOnlySet<int> selectedAgents,
            bool amountFilter,
            bool agentsFilter)
        {
            if (amountFilter)
            {
                var thr = agentThresholds.TryGetValue(b.Agent, out var t) ? t : thresholdBubble;
                if (b.Amount < thr) return false;
            }
            if (agentsFilter && !selectedAgents.Contains(b.Agent))
                return false;
            return true;
        }

        /// <summary>
        /// Filtro de bolhas idêntico ao da tela (`_bubblePassesFilters` /
        /// `_filteredBubbles` do app): amount ≥ threshold do agente (ou base)
        /// + agente selecionado, cada etapa condicionada à sua flag.
        /// </summary>
        public static List<BubbleStorageItem> ApplyBubbleFilter(
            IEnumerable<BubbleStorageItem> bubbles,
            int thresholdBubble,
            IReadOnlyDictionary<int, int> agentThresholds,
            IReadOnlySet<int> selectedAgents,
            bool amountFilter,
            bool agentsFilter) =>
            bubbles.Where(b => BubblePasses(b, thresholdBubble, agentThresholds,
                selectedAgents, amountFilter, agentsFilter)).ToList();

        /// <summary>Mesmo filtro lendo o spec cru (sem espelho tipado).</summary>
        public static List<BubbleStorageItem> ApplyBubbleFilter(
            IEnumerable<BubbleStorageItem> bubbles, JsonElement spec, int defaultThreshold) =>
            ApplyBubbleFilter(bubbles,
                ScreenSpec.Int(spec, "thresholdBubble", defaultThreshold),
                ScreenSpec.IntMap(spec, "agentThresholds"),
                ScreenSpec.IntSet(spec, "selectedAgents"),
                ScreenSpec.Bool(spec, "bubbleAmountFilter", true),
                ScreenSpec.Bool(spec, "bubbleAgentsFilter", true));

        // ---------------------------------------------------------------------
        // State JSON (objeto). Camadas já chegam recortadas ao escopo pelo
        // ScreenStateBuilder; aqui só projetamos para o contrato da Jev.
        // ---------------------------------------------------------------------

        /// <summary>
        /// Projeta o snapshot (já recortado ao escopo) no state JSON da Jev:
        /// bloco intraday + bloco diário. Sem config de tela, sem posição.
        /// </summary>
        public static JevStateDto BuildStateJson(
            string symbol,
            int timeFrame,
            MarketSnapshot snap,
            bool includeVolumeLevels = false,
            int maxCandles = 0,
            int maxBubbles = 0,
            int maxStructures = 6,
            int maxDailyCandles = 24,
            int maxDailyStructures = 4,
            int maxExtremes = 8,
            int maxZones = 6)
        {
            var candles = snap.Get<List<BarStorageItem>>(SnapshotLayers.Candles) ?? new();
            // A janela do contexto pode ser maior que a série crua enviada;
            // assim o movimento de 30 candles continua completo mesmo com o
            // payload enxuto.
            var contextCandles = candles;
            var bubbles = snap.Has(SnapshotLayers.Bubbles)
                ? snap.Get<List<BubbleStorageItem>>(SnapshotLayers.Bubbles) ?? new()
                : new();
            var volume = snap.Get<VolumeLevelStorageItem>(SnapshotLayers.Volume);
            var pivIntra = snap.Get<PivotStorageItem>(SnapshotLayers.PivotIntra);
            var extIntra = snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesIntra);
            var structs = snap.Get<List<StructureStorageItem>>(SnapshotLayers.Structures) ?? new();

            var dailyCandles = snap.Get<List<BarStorageItem>>(SnapshotLayers.DailyCandles) ?? new();
            var dailyVolume = snap.Get<VolumeLevelStorageItem>(SnapshotLayers.DailyVolume);
            var pivDaily = snap.Get<PivotStorageItem>(SnapshotLayers.PivotDaily);
            var extDaily = snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesDaily);

            var intraScope = snap.Get<DateWindow>(SnapshotLayers.ScopeIntraday);
            var dailyScope = snap.Get<DateWindow>(SnapshotLayers.ScopeDaily);

            var triggerKeys = TriggerKeys(snap);

            // Limite de payload (LookbackCandles/Bubbles): o snapshot resolve o
            // dia inteiro, mas o state não pode crescer junto — senão estoura o
            // max_tokens do provider. Mantém a janela mais recente e recorta o
            // escopo das estruturas/pivôs ao mesmo intervalo.
            var stateScope = intraScope;
            if (maxCandles > 0 && candles.Count > maxCandles)
            {
                candles = candles.Skip(candles.Count - maxCandles).ToList();
                stateScope = new DateWindow { From = candles[0].Date, To = candles[^1].Date };
            }

            var stateBubbles = bubbles.OrderBy(b => b.Date).ToList();
            if (maxBubbles > 0 && stateBubbles.Count > maxBubbles)
            {
                // Sempre preserva as bolhas do gatilho (podem ser antigas dentro
                // do mesmo candle) + as mais recentes até o limite.
                var keep = stateBubbles.Skip(stateBubbles.Count - maxBubbles)
                    .Select(BubbleKey).ToHashSet();
                if (triggerKeys != null)
                    foreach (var key in triggerKeys) keep.Add(key);
                stateBubbles = stateBubbles.Where(b => keep.Contains(BubbleKey(b))).ToList();
            }

            // Bolhas que passaram no filtro real da tela (sem o cluster extra
            // abaixo do threshold). Fallback: as bolhas do state.
            var filteredBubbles = snap.Raw.TryGetValue("bubblesFiltered", out var rawFiltered)
                && rawFiltered is List<BubbleStorageItem> filteredList
                    ? filteredList
                    : stateBubbles;
            var filteredKeys = filteredBubbles.Select(BubbleKey).ToHashSet();

            var asOf = candles.Count > 0
                ? candles[^1].Date.AddMinutes(timeFrame)
                : DateTime.MinValue;
            var tickSize = Defaults.GetTickSize(symbol);
            var lastPrice = candles.Count > 0 ? candles[^1].Close : 0;
            var levels = BuildLevelCandidates(
                contextCandles, pivIntra, pivDaily, extIntra, extDaily, tickSize);
            var nearestPivot = levels
                .Where(l => l.Kind == "pivot")
                .OrderBy(l => l.DistanceTicks)
                .FirstOrDefault();

            // Contexto destilado: o modelo recebe fatos pré-calculados, não
            // precisa fazer contas de volume/distância.
            var broadOptions = JevFeatureOptions.Broad with
            {
                ZoneMaxItems = Math.Max(maxZones, 8),
            };
            var context = JevContextAnalyzer.Build(
                symbol, timeFrame, contextCandles, volume, dailyVolume,
                pivIntra, pivDaily, extIntra, extDaily,
                filteredBubbles, stateBubbles, asOf, broadOptions);
            var strictMove = JevContextAnalyzer.BuildRecentMove(
                contextCandles, tickSize, JevFeatureOptions.Strict);
            var strictConfluence = JevContextAnalyzer.EvaluateConfluence(
                context.Zones, strictMove, JevFeatureOptions.Strict, lastPrice);
            var contextDto = ToContext(context, strictConfluence);

            var levelContext = context.Zones
                .Take(maxZones)
                .Select(ToLevelContext)
                .ToList();

            var intraStructures = FilterIntradayStructures(structs, timeFrame, stateScope);
            if (maxStructures > 0 && intraStructures.Count > maxStructures)
                intraStructures = intraStructures.TakeLast(maxStructures).ToList();

            var dailyStateCandles = dailyCandles;
            if (maxDailyCandles > 0 && dailyStateCandles.Count > maxDailyCandles)
                dailyStateCandles = dailyStateCandles.TakeLast(maxDailyCandles).ToList();

            var dailyStructures = FilterDailyStructures(structs, dailyScope, dailyCandles);
            if (maxDailyStructures > 0 && dailyStructures.Count > maxDailyStructures)
                dailyStructures = dailyStructures.TakeLast(maxDailyStructures).ToList();

            var intraday = new JevIntradayDto
            {
                Scope = Range(stateScope, candles),
                Context = contextDto,
                Candles = candles.Select(ToCandle).ToList(),
                VolumeProfile = ToVolumeProfile(volume, includeVolumeLevels),
                Bubbles = stateBubbles
                    .Select(b => ToBubble(b, triggerKeys, filteredKeys)).ToList(),
                Pivots = ToPivots(pivIntra, candles),
                TopsValleys = ToExtremes(extIntra, lastPrice, tickSize, maxExtremes),
                Structures = intraStructures
                    .Select(s => ToStructure(s, withTimeframe: true)).ToList(),
                NearestPivot = nearestPivot == null ? null : ToNearestPivot(nearestPivot),
                LevelContext = levelContext,
            };

            var daily = new JevDailyDto
            {
                Scope = Range(dailyScope, dailyCandles),
                Candles = dailyStateCandles.Select(ToCandle).ToList(),
                VolumeProfile = ToVolumeProfile(dailyVolume, includeVolumeLevels),
                Pivots = ToPivots(pivDaily, dailyCandles),
                TopsValleys = ToExtremes(extDaily, lastPrice, tickSize, maxExtremes),
                Structures = dailyStructures
                    .Select(s => ToStructure(s, withTimeframe: false)).ToList(),
            };

            return new JevStateDto
            {
                Symbol = symbol,
                StrategyTimeframe = timeFrame,
                Intraday = intraday,
                Daily = daily,
            };
        }

        private static JevRangeDto Range(DateWindow? window, IReadOnlyList<BarStorageItem> fallback) => new()
        {
            From = window?.From ?? (fallback.Count > 0 ? fallback[0].Date : default),
            To = window?.To ?? (fallback.Count > 0 ? fallback[^1].Date : default),
        };

        /// <summary>
        /// Estruturas intraday do TF da estratégia dentro do escopo do bloco.
        /// Sem escopo resolvido, devolve vazio: nunca cai no histórico global.
        /// Deduplica por data (a última impressão daquela data vence).
        /// </summary>
        private static List<StructureStorageItem> FilterIntradayStructures(
            IReadOnlyList<StructureStorageItem>? structs,
            int timeFrame,
            DateWindow? scope)
        {
            if (structs == null || structs.Count == 0 || scope == null) return new();

            var byDate = new Dictionary<DateTime, StructureStorageItem>();
            foreach (var s in structs.Where(s =>
                         s.TimeFrame != 1440 && s.TimeFrame == timeFrame &&
                         s.Date >= scope.From && s.Date <= scope.To))
                byDate[s.Date] = s;
            return byDate.Values.OrderBy(s => s.Date).ToList();
        }

        /// <summary>
        /// Estruturas diárias (TF 1440) dentro do escopo diário. Compara por
        /// dia (não por horário) e mantém uma única estrutura por dia: a
        /// estrutura diária é a mesma durante todo o dia.
        /// </summary>
        private static List<StructureStorageItem> FilterDailyStructures(
            IReadOnlyList<StructureStorageItem>? structs,
            DateWindow? scope,
            IReadOnlyList<BarStorageItem> dailyCandles)
        {
            if (structs == null || structs.Count == 0) return new();
            DateTime? from = scope?.From.Date ??
                (dailyCandles.Count > 0 ? dailyCandles[0].Date.Date : null);
            DateTime? to = scope?.To.Date ??
                (dailyCandles.Count > 0 ? dailyCandles[^1].Date.Date : null);

            var byDay = new Dictionary<DateTime, StructureStorageItem>();
            foreach (var s in structs.Where(s => s.TimeFrame == 1440))
            {
                var day = s.Date.Date;
                if (from is { } f && day < f) continue;
                if (to is { } t && day > t) continue;
                byDay[day] = s;
            }
            return byDay.Values.OrderBy(s => s.Date).ToList();
        }

        private static JevCandleDto ToCandle(BarStorageItem c) => new()
        {
            Date = c.Date, Open = c.Open, High = c.High, Low = c.Low,
            Close = c.Close, Volume = c.Volume, Vwap = c.VwapPrice,
        };

        private static JevBubbleDto ToBubble(
            BubbleStorageItem b,
            IReadOnlySet<string>? triggerKeys = null,
            IReadOnlySet<string>? filteredKeys = null)
        {
            var key = BubbleKey(b);
            var trigger = triggerKeys?.Contains(key) == true;
            var source = trigger ? "trigger"
                : filteredKeys?.Contains(key) == true ? "filter"
                : "cluster";
            return new JevBubbleDto
            {
                Date = b.Date, Agent = AgentName(b.Agent),
                Side = b.ActionType == Ticks2.ActionType.Buy ? "buy" : "sell",
                Amount = b.Amount, Price = b.Price,
                IsTrigger = trigger,
                Source = source,
            };
        }

        /// <summary>Chave estável de uma bolha para marcar as do gatilho.</summary>
        public static string BubbleKey(BubbleStorageItem b) =>
            $"{b.Date:O}|{b.Price:F2}|{b.Agent}|{b.Amount}|{b.ActionType}";

        private static IReadOnlySet<string>? TriggerKeys(MarketSnapshot snap) =>
            snap.Raw.TryGetValue("triggerBubbleKeys", out var v) && v is IReadOnlySet<string> keys
                ? keys
                : null;

        private static IReadOnlySet<string>? FilteredBubbleKeys(MarketSnapshot snap) =>
            snap.Raw.TryGetValue("bubblesFiltered", out var v) &&
            v is IEnumerable<BubbleStorageItem> filtered
                ? filtered.Select(BubbleKey).ToHashSet()
                : null;

        private static int ArrayLength(JsonElement parent, string name, int fallback) =>
            parent.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Array
                ? value.GetArrayLength()
                : fallback;

        /// <summary>
        /// Nome do agente para a Jev (ex.: "BGC LIQUIDEZ DTVM") no lugar do
        /// código numérico. Agente fora do enum mantém o número com prefixo.
        /// </summary>
        public static string AgentName(int agent)
        {
            var value = (Ticks2.Agents)agent;
            return System.Enum.IsDefined(value) ? value.Description() : $"AGENTE {agent}";
        }

        private static JevStructureDto ToStructure(StructureStorageItem s, bool withTimeframe) => new()
        {
            Date = s.Date,
            Timeframe = withTimeframe ? s.TimeFrame : null,
            Up = s.UpBorder, Down = s.DownBorder,
            UpAux = s.UpAuxBorder, DownAux = s.DownAuxBorder,
        };

        /// <summary>
        /// POC + delta total + níveis do escopo. Os níveis são opcionais para
        /// reduzir o payload; POC/delta continuam disponíveis nos critérios.
        /// </summary>
        private static JevVolumeProfileDto? ToVolumeProfile(
            VolumeLevelStorageItem? volume,
            bool includeLevels)
        {
            var levels = volume?.Volumes;
            if (levels == null || levels.Count == 0) return null;
            var poc = levels.OrderByDescending(l => l.Total).First();
            return new JevVolumeProfileDto
            {
                Poc = poc.Price,
                DeltaTotal = levels.Sum(l => l.BuyVolume) - levels.Sum(l => l.SellVolume),
                Levels = includeLevels
                    ? levels.OrderBy(l => l.Price).Select(l => new JevVolumeLevelDto
                    {
                        Price = l.Price, Total = l.Total,
                        Buy = l.BuyVolume, Sell = l.SellVolume, Delta = l.Delta,
                    }).ToList()
                    : new List<JevVolumeLevelDto>(),
            };
        }

        /// <summary>
        /// Pivôs com `touched_at` = data do 1º candle do escopo cujo
        /// [low,high] contém o nível (null se nunca tocou).
        /// </summary>
        private static List<JevPivotDto> ToPivots(PivotStorageItem? pivot, IReadOnlyList<BarStorageItem> bars)
        {
            var out_ = new List<JevPivotDto>();
            if (pivot == null) return out_;
            foreach (var lvl in pivot.Levels)
            {
                DateTime? touched = null;
                foreach (var b in bars)
                {
                    if (b.Low <= lvl.Value && lvl.Value <= b.High)
                    {
                        touched = b.Date;
                        break;
                    }
                }
                out_.Add(new JevPivotDto
                {
                    Source = pivot.Source, Level = lvl.Key, Value = lvl.Value, TouchedAt = touched,
                });
            }
            return out_;
        }

        private static List<JevExtremeDto> ToExtremes(
            ExtremeStorageItem? item,
            double lastPrice,
            double tickSize,
            int maxItems)
        {
            var extremes = (item?.Extremes ?? new List<ExtremePoint>())
                .Where(e => e.Type is ExtremeType.Top or ExtremeType.Valley)
                .Select(e => new JevExtremeDto
                {
                    Type = e.Type == ExtremeType.Top ? "topo" : "vale",
                    Price = e.Position,
                    Strength = e.Strength,
                    Confidence = e.Confidence,
                    Width = e.Width,
                    Start = e.StartPosition,
                    End = e.EndPosition,
                    DistanceTicks = tickSize > 0
                        ? Math.Abs(lastPrice - e.Position) / tickSize
                        : 0,
                    Side = e.Type == ExtremeType.Valley ? "support" : "resistance",
                    IsMajor = e.Confidence >= 0.50,
                })
                .OrderBy(e => e.DistanceTicks)
                .ToList();
            return maxItems > 0 && extremes.Count > maxItems
                ? extremes.Take(maxItems).ToList()
                : extremes;
        }

        /// <summary>Nível candidato (pivô/vale/topo) com distância ao preço.</summary>
        public sealed record JevLevelCandidate(
            string Kind, string Source, string Level, double Value,
            double DistanceTicks, string Side, DateTime? TouchedAt, bool Reclaimed);

        /// <summary>
        /// Níveis relevantes do state: pivôs intraday/diário + topos/vales
        /// intraday/diário. Ordenados por distância ao último close.
        /// Puro, sem I/O.
        /// </summary>
        public static List<JevLevelCandidate> BuildLevelCandidates(
            IReadOnlyList<BarStorageItem> candles,
            PivotStorageItem? pivIntra,
            PivotStorageItem? pivDaily,
            ExtremeStorageItem? extIntra,
            ExtremeStorageItem? extDaily,
            double tickSize)
        {
            var list = new List<JevLevelCandidate>();
            if (candles.Count == 0 || tickSize <= 0) return list;
            var last = candles[^1].Close;
            var recentFrom = candles[^1].Date.AddMinutes(-LevelClusterMinutes);

            void AddPivot(PivotStorageItem? pivot, string source)
            {
                if (pivot == null) return;
                foreach (var lvl in pivot.Levels)
                {
                    var side = lvl.Key.StartsWith("S", StringComparison.OrdinalIgnoreCase)
                        ? "support"
                        : lvl.Key.StartsWith("R", StringComparison.OrdinalIgnoreCase)
                            ? "resistance" : "neutral";
                    var (touched, reclaimed) = TouchState(candles, lvl.Value, side, recentFrom);
                    list.Add(new JevLevelCandidate(
                        "pivot", source, lvl.Key, lvl.Value,
                        Math.Abs(last - lvl.Value) / tickSize, side, touched, reclaimed));
                }
            }

            void AddExtremes(ExtremeStorageItem? item, string source)
            {
                if (item == null) return;
                foreach (var e in item.Extremes.Where(e => e.Type is ExtremeType.Top or ExtremeType.Valley))
                {
                    var kind = e.Type == ExtremeType.Valley ? "vale" : "topo";
                    var side = e.Type == ExtremeType.Valley ? "support" : "resistance";
                    var (touched, reclaimed) = TouchState(candles, e.Position, side, recentFrom);
                    list.Add(new JevLevelCandidate(
                        kind, source, kind, e.Position,
                        Math.Abs(last - e.Position) / tickSize, side, touched, reclaimed));
                }
            }

            AddPivot(pivIntra, pivIntra?.Source ?? "D-1");
            AddPivot(pivDaily, pivDaily?.Source ?? "W-1");
            AddExtremes(extIntra, "intra");
            AddExtremes(extDaily, "diario");
            return list.OrderBy(l => l.DistanceTicks).ToList();
        }

        private static (DateTime? Touched, bool Reclaimed) TouchState(
            IReadOnlyList<BarStorageItem> candles, double value, string side, DateTime recentFrom)
        {
            DateTime? first = null;
            var recent = false;
            for (var i = 0; i < candles.Count; i++)
            {
                var b = candles[i];
                if (b.Low > value || value > b.High) continue;
                first ??= b.Date;
                if (b.Date >= recentFrom) recent = true;
            }
            if (first == null) return (null, false);
            var last = candles[^1].Close;
            var reclaimed = side switch
            {
                "support" => recent && last > value,
                "resistance" => recent && last < value,
                _ => recent && Math.Abs(last - value) <= 2 * Math.Max(1, value * 0.0001),
            };
            return (first, reclaimed);
        }

        private static JevNearestPivotDto ToNearestPivot(JevLevelCandidate c) => new()
        {
            Source = c.Source, Level = c.Level, Value = c.Value,
            DistanceTicks = c.DistanceTicks, Side = c.Side,
            TouchedAt = c.TouchedAt, Reclaimed = c.Reclaimed,
        };

        private static JevLevelContextDto ToLevelContext(JevZoneFeature zone) => new()
        {
            Kind = zone.Kind,
            Source = zone.Source,
            Level = zone.Level,
            Value = zone.Price,
            Side = zone.Side,
            DistanceTicks = zone.DistanceTicks,
            TouchedAt = zone.TouchedAt,
            Reclaimed = zone.Reclaimed,
            VolumeRank = zone.VolumeRank,
            LowVolume = zone.LowVolume,
            ZoneLow = zone.ZoneLow,
            ZoneHigh = zone.ZoneHigh,
            ZoneStrength = zone.ZoneStrength,
            ZoneConfidence = zone.ZoneConfidence,
            IsMajor = zone.IsMajor,
            Bubbles = ToBubbleSummary(zone.FilteredBubbles),
            ClusterBubbles = ToBubbleSummary(zone.ClusterBubbles),
            Absorption = zone.Absorption,
        };

        private static JevBubbleSummaryDto ToBubbleSummary(JevBubbleSummary s) => new()
        {
            Count = s.Count,
            BuyCount = s.BuyCount,
            SellCount = s.SellCount,
            BuyAmount = s.BuyAmount,
            SellAmount = s.SellAmount,
            NetAmount = s.NetAmount,
        };

        private static JevContextDto ToContext(
            JevContextFeatures context, JevConfluence strict)
        {
            var broad = context.Confluence;
            return new JevContextDto
            {
                Price = context.Price,
                TickSize = context.TickSize,
                RecentMove = new JevRecentMoveDto
                {
                    Direction = context.RecentMove.Direction,
                    Candles = context.RecentMove.Candles,
                    ChangeTicks = context.RecentMove.ChangeTicks,
                    RangeTicks = context.RecentMove.RangeTicks,
                    AtrTicks = context.RecentMove.AtrTicks,
                    Efficiency = context.RecentMove.Efficiency,
                    Strength = context.RecentMove.Strong ? "forte" : "fraca",
                },
                DailyVolume = ToVolumeSummary(context.DailyVolume),
                IntradayVolume = ToVolumeSummary(context.IntradayVolume),
                Confluence = new JevConfluenceDto
                {
                    AtDailyZone = broad.AtDailyZone,
                    DailyZoneType = broad.DailyZoneType,
                    OppositeDailyZone = broad.OppositeDailyZone,
                    OppositeDailyZonePrice = broad.OppositeDailyZonePrice,
                    OppositeDailyZoneDistanceTicks = broad.OppositeDailyZoneDistanceTicks,
                    OppositeDailyZoneInRange = broad.OppositeDailyZoneInRange,
                    LowVolumeNearValley = broad.LowVolumeNearValley,
                    LowVolumeValleyRank = broad.LowVolumeValleyRank,
                    LowVolumeValleyDistanceTicks = broad.LowVolumeValleyDistanceTicks,
                    FilteredBubblesAtZone = broad.FilteredBubblesAtZone,
                    FilteredBubbleCount = broad.FilteredBubbleCount,
                    AbsorptionSide = broad.AbsorptionSide,
                    PriorMove = broad.PriorMove,
                    PriorMoveDirection = broad.PriorMoveDirection,
                    PriorMoveStrong = broad.PriorMoveStrong,
                    SetupContexto = JevContextAnalyzer.IsSetupContexto(broad),
                    SetupConfirmacao = JevContextAnalyzer.IsSetupConfirmacao(strict),
                },
            };
        }

        private static JevVolumeSummaryDto? ToVolumeSummary(JevVolumeSummary? summary)
        {
            if (summary == null) return null;
            return new JevVolumeSummaryDto
            {
                Poc = summary.Poc,
                Vah = summary.Vah,
                Val = summary.Val,
                DeltaTotal = summary.DeltaTotal,
                Total = summary.Total,
                PriceRank = summary.PriceRank,
                PriceInLowVolume = summary.PriceInLowVolume,
                NearestLvn = ToVolumeNode(summary.NearestLvn),
                Hvn = summary.Hvn.Select(ToVolumeNode).Where(n => n != null).Cast<JevVolumeNodeDto>().ToList(),
                Lvn = summary.Lvn.Select(ToVolumeNode).Where(n => n != null).Cast<JevVolumeNodeDto>().ToList(),
            };
        }

        private static JevVolumeNodeDto? ToVolumeNode(JevVolumeNode? node)
        {
            if (node == null) return null;
            return new JevVolumeNodeDto
            {
                Price = node.Price,
                Total = node.Total,
                Share = node.Share,
                DistanceTicks = node.DistanceTicks,
            };
        }

        /// <summary>
        /// Une as bolhas do cluster de níveis na camada do snapshot. Chamado
        /// tanto pela strategy quanto pelo endpoint avulso para manter o state
        /// idêntico nos dois caminhos. Mutação explícita no snapshot.
        /// </summary>
        public static void EnsureLevelCluster(MarketSnapshot snap, DateTime asOf)
        {
            var extra = SelectLevelClusterBubbles(snap, asOf);
            if (extra.Count == 0) return;
            var current = snap.Get<List<BubbleStorageItem>>(SnapshotLayers.Bubbles) ?? new();
            snap.Layers[SnapshotLayers.Bubbles] = MergeBubbles(current, extra);
        }

        /// <summary>
        /// Une listas de bolhas sem duplicar (mesma bolha = data, preço,
        /// agente, amount e ação) e ordena por data. Puro e testável.
        /// </summary>
        public static List<BubbleStorageItem> MergeBubbles(
            IEnumerable<BubbleStorageItem> current,
            IEnumerable<BubbleStorageItem> extra) =>
            current.Concat(extra)
                .GroupBy(b => (b.Date, b.Price, b.Agent, b.Amount, b.ActionType))
                .Select(g => g.First())
                .OrderBy(b => b.Date)
                .ToList();

        /// <summary>
        /// Bolhas do pool bruto dentro do raio dos níveis relevantes (mesmo
        /// abaixo do threshold da tela). Usado pela strategy antes do state.
        /// Puro, sem I/O; devolve vazio se o pool não estiver no snapshot.
        /// </summary>
        public static List<BubbleStorageItem> SelectLevelClusterBubbles(
            MarketSnapshot snap, DateTime asOf)
        {
            if (!snap.Raw.TryGetValue("bubblesPool", out var raw) ||
                raw is not List<BubbleStorageItem> pool || pool.Count == 0)
                return new();

            var candles = snap.Get<List<BarStorageItem>>(SnapshotLayers.Candles) ?? new();
            if (candles.Count == 0) return new();
            var symbol = candles[^1].Symbol;
            var tickSize = Defaults.GetTickSize(symbol);
            var levels = BuildLevelCandidates(
                candles,
                snap.Get<PivotStorageItem>(SnapshotLayers.PivotIntra),
                snap.Get<PivotStorageItem>(SnapshotLayers.PivotDaily),
                snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesIntra),
                snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesDaily),
                tickSize)
                .Where(l => l.DistanceTicks <= 50 ||
                    (l.TouchedAt is { } touched && touched >= asOf.AddMinutes(-LevelClusterMinutes)))
                .ToList();
            if (levels.Count == 0) return new();

            var from = asOf.AddMinutes(-LevelClusterMinutes);
            var radius = LevelClusterTicks * tickSize;
            var selected = new List<BubbleStorageItem>();
            foreach (var level in levels)
            {
                selected.AddRange(pool
                    .Where(b => b.Date >= from && b.Date <= asOf &&
                        Math.Abs(b.Price - level.Value) <= radius)
                    .OrderByDescending(b => b.Amount)
                    .ThenByDescending(b => b.Date)
                    .Take(LevelClusterMaxBubbles));
            }
            return selected
                .GroupBy(BubbleKey)
                .Select(g => g.First())
                .OrderBy(b => b.Date)
                .ToList();
        }

        /// <summary>
        /// Mede o tamanho (chars) de cada bloco do state JSON, para a auditoria
        /// (issue #23): mostra qual bloco domina tokens. Puro, sem I/O.
        /// </summary>
        public static Dictionary<string, int> MeasureBlocks(
            string symbol, int timeFrame, MarketSnapshot snap,
            int maxCandles = 30, int maxBubbles = 24)
        {
            var state = BuildStateJson(symbol, timeFrame, snap,
                maxCandles: maxCandles, maxBubbles: maxBubbles);
            int Len(object v) => JsonSerializer.Serialize(v).Length;
            return new Dictionary<string, int>
            {
                ["intraday.context"] = Len(state.Intraday.Context),
                ["intraday.candles"] = Len(state.Intraday.Candles),
                ["intraday.bubbles"] = Len(state.Intraday.Bubbles),
                ["intraday.volume"] = Len(state.Intraday.VolumeProfile),
                ["intraday.pivots"] = Len(state.Intraday.Pivots),
                ["intraday.extremes"] = Len(state.Intraday.TopsValleys),
                ["intraday.structures"] = Len(state.Intraday.Structures),
                ["intraday.nearest_pivot"] = Len(state.Intraday.NearestPivot),
                ["intraday.level_context"] = Len(state.Intraday.LevelContext),
                ["daily.candles"] = Len(state.Daily.Candles),
                ["daily.volume"] = Len(state.Daily.VolumeProfile),
                ["daily.pivots"] = Len(state.Daily.Pivots),
                ["daily.extremes"] = Len(state.Daily.TopsValleys),
                ["daily.structures"] = Len(state.Daily.Structures),
            };
        }

        /// <summary>Serializa um valor da auditoria em JSON; null/undefined → null.</summary>
        private static JsonElement? Je(object? v)
        {
            if (v is null) return null;
            if (v is JsonElement je) return je.ValueKind == JsonValueKind.Undefined ? null : je;
            return JsonSerializer.SerializeToElement(v);
        }

        /// <summary>Lê um dado cru de apoio (snap.Raw) ou null.</summary>
        private static object? Raw(MarketSnapshot snap, string key) =>
            snap.Raw.TryGetValue(key, out var v) ? v : null;

        /// <summary>
        /// Projeta candles para a auditoria: só os campos que o state/JEV
        /// consome (OHLCV + VWAP). Evita o <c>VolumeLevel</c> por barra, que
        /// não é usado no prompt e inflaria o arquivo (~2 MB por chamada).
        /// </summary>
        public static JsonElement ProjectCandles(IEnumerable<BarStorageItem> candles) =>
            JsonSerializer.SerializeToElement(candles.Select(c => new
            {
                c.Date, c.Symbol, c.TimeFrame, c.Open, c.High, c.Low, c.Close, c.Volume, c.VwapPrice,
            }).ToList());

        /// <summary>
        /// Monta o registro de auditoria de UMA chamada (o que chegou,
        /// o que filtrou, payload, chamada, resposta). Puro, sem I/O e sem
        /// chave — a gravação em Data/JEV vive no JevAuditStore.
        /// <paramref name="state"/> é o state JSON já serializado (usado só
        /// para métricas); <paramref name="payload"/> é o corpo HTTP exato.
        /// </summary>
        public static JevAuditRecord BuildAudit(
            string auditId,
            DateTime at,
            DateTime triggerAt,
            string triggerType,
            string symbol,
            int timeFrame,
            DateTime displayDate,
            string origin,
            string model,
            string snapshotHash,
            int lookbackCandles,
            int lookbackBubbles,
            MarketSnapshot snap,
            string state,
            string payload,
            int httpStatus,
            long latencyMs,
            int attempts,
            string bodyRaw,
            JevAnalysisResult? parsed,
            string? errorType = null,
            string? error = null)
        {
            var header = snap.Get<HeaderView>(SnapshotLayers.Header);
            var candles = snap.Get<List<BarStorageItem>>(SnapshotLayers.Candles) ?? new();
            var bubbles = snap.Has(SnapshotLayers.Bubbles)
                ? snap.Get<List<BubbleStorageItem>>(SnapshotLayers.Bubbles) ?? new()
                : new();
            var volume = snap.Get<VolumeLevelStorageItem>(SnapshotLayers.Volume);
            var extIntra = snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesIntra);
            var extDaily = snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesDaily);
            var structs = snap.Get<List<StructureStorageItem>>(SnapshotLayers.Structures);
            var pivIntra = snap.Get<PivotStorageItem>(SnapshotLayers.PivotIntra);
            var pivDaily = snap.Get<PivotStorageItem>(SnapshotLayers.PivotDaily);
            var dailyCandles = snap.Get<List<BarStorageItem>>(SnapshotLayers.DailyCandles) ?? new();
            var dailyVolume = snap.Get<VolumeLevelStorageItem>(SnapshotLayers.DailyVolume);

            var layersOn = new List<string>();
            foreach (var key in new[]
                { SnapshotLayers.Candles, SnapshotLayers.Bubbles,
                  SnapshotLayers.Volume, SnapshotLayers.Structures, SnapshotLayers.ExtremesIntra,
                  SnapshotLayers.ExtremesDaily, SnapshotLayers.PivotIntra, SnapshotLayers.PivotDaily,
                  SnapshotLayers.Vwap, SnapshotLayers.DailyCandles, SnapshotLayers.DailyVolume })
                if (snap.Has(key)) layersOn.Add(key);

            var sentCandles = candles.Count;
            var sentBubbles = bubbles.Count;
            var sentStructures = 0;
            var sentExtremesIntra = 0;
            var sentExtremesDaily = 0;
            var sentPivotsIntra = 0;
            var sentPivotsDaily = 0;
            JsonElement? sentBubblesJson = null;
            try
            {
                using var stateDoc = JsonDocument.Parse(state);
                if (stateDoc.RootElement.ValueKind == JsonValueKind.Object &&
                    stateDoc.RootElement.TryGetProperty("intraday", out var intra) &&
                    intra.ValueKind == JsonValueKind.Object)
                {
                    sentCandles = ArrayLength(intra, "candles", sentCandles);
                    sentBubbles = ArrayLength(intra, "bubbles", sentBubbles);
                    sentStructures = ArrayLength(intra, "structures", 0);
                    sentExtremesIntra = ArrayLength(intra, "tops_valleys", 0);
                    sentPivotsIntra = ArrayLength(intra, "pivots", 0);
                    if (intra.TryGetProperty("bubbles", out var bubblesElement) &&
                        bubblesElement.ValueKind == JsonValueKind.Array)
                        sentBubblesJson = bubblesElement.Clone();
                }
                if (stateDoc.RootElement.TryGetProperty("daily", out var daily) &&
                    daily.ValueKind == JsonValueKind.Object)
                {
                    sentStructures += ArrayLength(daily, "structures", 0);
                    sentExtremesDaily = ArrayLength(daily, "tops_valleys", 0);
                    sentPivotsDaily = ArrayLength(daily, "pivots", 0);
                }
            }
            catch
            {
                // state sempre é JSON; se falhar, mantém as contagens da camada.
            }

            var blocks = MeasureBlocks(symbol, timeFrame, snap,
                lookbackCandles, lookbackBubbles);
            return new JevAuditRecord
            {
                Id = auditId,
                At = at,
                TriggerAt = triggerAt,
                TriggerType = triggerType ?? "",
                Symbol = symbol,
                TimeFrame = timeFrame,
                DisplayDate = displayDate.Date,
                Origin = origin,
                Model = model,
                SnapshotHash = snapshotHash ?? "",
                Input = new JevAuditInput
                {
                    CandlesReceived = candles.Count,
                    BubblesPoolTotal = header?.BubblesTotalDay ?? bubbles.Count,
                    BubblesAfterScreenFilter = header?.BubblesVisibleDay ?? bubbles.Count,
                    ThresholdBubble = header?.ThresholdBubble ?? 0,
                    LookbackCandles = lookbackCandles,
                    LookbackBubbles = lookbackBubbles,
                    LayersOn = layersOn,
                    // JSONs reais de tudo que chegou para tratar/filtrar/ordenar.
                    ScreenSpec = Je(Raw(snap, "spec")),
                    Header = Je(header),
                    Candles = ProjectCandles(candles),
                    BubblesPool = Je(Raw(snap, "bubblesPool")),
                    BubblesFiltered = Je(Raw(snap, "bubblesFiltered")),
                    BubblesSent = sentBubblesJson != null
                        ? Je(sentBubblesJson)
                        : Je(snap.Has(SnapshotLayers.Bubbles)
                            ? bubbles.Select(b => ToBubble(b, TriggerKeys(snap),
                                FilteredBubbleKeys(snap))).ToList()
                            : null),
                    Volume = Je(volume),
                    ExtremesIntra = Je(extIntra),
                    ExtremesDaily = Je(extDaily),
                    Structures = Je(structs),
                    PivotIntra = Je(pivIntra),
                    PivotDaily = Je(pivDaily),
                    Vwap = snap.Num(SnapshotLayers.Vwap),
                    DailyCandles = Je(dailyCandles),
                    DailyVolume = Je(dailyVolume),
                },
                Filtered = new JevAuditFiltered
                {
                    CandlesSent = sentCandles,
                    BubblesSent = sentBubbles,
                    VolumeLevels = volume?.Volumes.Count ?? 0,
                    ExtremesIntraCount = sentExtremesIntra,
                    ExtremesDailyCount = sentExtremesDaily,
                    StructuresCount = sentStructures,
                    PivotIntraLevels = sentPivotsIntra,
                    PivotDailyLevels = sentPivotsDaily,
                    HasVwap = snap.Num(SnapshotLayers.Vwap) is not null,
                    StateChars = state.Length,
                    EstimatedTokens = JevStateBudget.EstimateTokens(state),
                    BudgetOk = !JevStateBudget.ExceedsPerQuestionLimit(state),
                    StateBlocks = blocks,
                },
                Payload = payload,
                Call = new JevAuditCall
                {
                    HttpStatus = httpStatus,
                    LatencyMs = latencyMs,
                    Attempts = attempts,
                    ErrorType = errorType ?? "",
                    Error = error ?? "",
                },
                // No sucesso mapeia a resposta interpretada; na falha o Answer
                // fica default (só o corpo cru do erro é preservado).
                Answer = parsed == null
                    ? new JevAuditAnswer { BodyRaw = bodyRaw ?? "" }
                    : new JevAuditAnswer
                    {
                        BodyRaw = bodyRaw ?? "",
                        Direcao = parsed.Direcao,
                        DirecaoConfidence = parsed.DirecaoConfidence,
                        DirecaoNet = parsed.DirecaoNet,
                        DirecaoConsenso = parsed.DirecaoConsenso,
                        DirecaoCobertura = parsed.DirecaoCobertura,
                        DirecaoMotivo = parsed.DirecaoMotivo,
                        Leitura = parsed.Leitura,
                        LeituraConfidence = parsed.LeituraConfidence,
                        PerdaDeForca = parsed.PerdaDeForca,
                        Absorcao = parsed.Absorcao,
                        Contexto = parsed.Contexto,
                        SetupContexto = parsed.SetupContexto,
                        SetupConfirmacao = parsed.SetupConfirmacao,
                        NivelChave = parsed.NivelChave,
                        Evidencias = new List<string>(parsed.Evidencias),
                        ConfiancaOperavel = parsed.ConfiancaOperavel,
                        Criteria = new Dictionary<string, double>(parsed.Criteria),
                        Proximidade = parsed.Proximidade,
                        ProximidadeTicks = parsed.ProximidadeTicks,
                        ProximidadeRef = parsed.ProximidadeRef,
                        InputTokens = parsed.InputTokens,
                        OutputTokens = parsed.OutputTokens,
                    },
            };
        }

        /// <summary>
        /// Extrai <c>detail.error_type</c> do corpo de erro do TypeSafe
        /// (ex.: max_tokens_exceeded). Corpo vazio/malformado → "". Puro.
        /// </summary>
        public static string ParseErrorType(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "";
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == JsonValueKind.Object &&
                    detail.TryGetProperty("error_type", out var et) &&
                    et.ValueKind == JsonValueKind.String)
                    return et.GetString() ?? "";
            }
            catch
            {
                // corpo não-JSON: sem error_type estruturado.
            }
            return "";
        }

        /// <summary>
        /// Dimensões de direção com perguntas espelhadas (alta/baixa). O id da
        /// dimensão é o mesmo da pergunta de alta para preservar os pesos já
        /// gravados em Data/jev_weights.json.
        /// </summary>
        public static readonly JevDirectionDimension[] DirectionDimensions =
        {
            new("candles_alta", "candles_alta", "candles_baixa"),
            new("acima_vwap", "acima_vwap", "abaixo_vwap"),
            new("estrutura_alta", "estrutura_alta", "estrutura_baixa"),
            new("acima_poc", "acima_poc", "abaixo_poc"),
            new("delta_positivo", "delta_positivo", "delta_negativo"),
            new("bolhas_compra", "bolhas_compra", "bolhas_venda"),
            new("defende_suporte", "defende_suporte", "rejeita_resistencia"),
            new("reversao_suporte", "reversao_suporte", "reversao_resistencia"),
            new("exaustao_queda", "exaustao_queda", "exaustao_alta"),
            new("diario_alta", "diario_alta", "diario_baixa"),
        };

        /// <summary>IDs de direção (8 pares alta/baixa = 16 perguntas cruas).</summary>
        public static readonly string[] DirectionIndicators =
            DirectionDimensions.SelectMany(d => new[] { d.BullId, d.BearId }).ToArray();

        /// <summary>IDs das dimensões de direção (usados pelos pesos).</summary>
        public static readonly string[] DirectionDimensionIds =
            DirectionDimensions.Select(d => d.Id).ToArray();

        /// <summary>IDs dos indicadores de operabilidade.</summary>
        public static readonly string[] OperabilityIndicators =
        {
            "sem_chop", "amplitude_suficiente", "sem_compressao_topovale",
            "referencia_clara", "sem_conflito_intra_diario",
        };

        private static readonly Dictionary<string, string> DirectionLabels = new()
        {
            ["candles_alta"] = "candles",
            ["acima_vwap"] = "VWAP",
            ["estrutura_alta"] = "estrutura",
            ["acima_poc"] = "POC",
            ["delta_positivo"] = "delta",
            ["bolhas_compra"] = "bolhas",
            ["defende_suporte"] = "suporte/resistência",
            ["reversao_suporte"] = "reversão S/R",
            ["exaustao_queda"] = "exaustão",
            ["diario_alta"] = "diário",
        };

        private const string NoulScale =
            " Responda de 0 a 1: 0 = afirmação claramente falsa; " +
            "0.5 = inconclusiva ou dado insuficiente; 1 = afirmação claramente verdadeira. " +
            "Não presuma direção; use apenas o state.";

        private static object Noul(
            string instructions, Dictionary<string, string>? criteria = null)
        {
            var question = new Dictionary<string, object>
            {
                ["type"] = "noul",
                ["instructions"] = instructions + NoulScale,
            };
            if (criteria != null) question["criteria"] = criteria;
            return question;
        }

        private static object Choice(
            string instructions, Dictionary<string, string> criteria) => new Dictionary<string, object>
        {
            ["type"] = "choice",
            ["instructions"] = instructions,
            ["criteria"] = criteria,
        };

        private static object Score(string instructions, string[] criteria) => new Dictionary<string, object>
        {
            ["type"] = "score",
            ["instructions"] = instructions,
            ["criteria"] = criteria,
        };

        /// <summary>
        /// Perguntas atômicas da leitura simples. O código já entrega o
        /// contexto destilado (zonas, volume, bolhas filtradas, movimento);
        /// a Jev só confirma a leitura e a perda de força.
        /// </summary>
        public static Dictionary<string, object> BuildQuestions() => new()
        {
            ["evidencia_suficiente"] = Noul(
                "O `intraday.context` contém evidência suficiente para escolher uma das opções de `leitura`? " +
                "Use `intraday.context.confluence`, `intraday.context.recent_move` e `intraday.level_context`.",
                new Dictionary<string, string>
                {
                    ["true"] = "Há zona diária, volume e bolhas filtradas que sustentam uma leitura.",
                    ["false"] = "Os dados são ambíguos, insuficientes ou conflitantes.",
                }),
            ["leitura"] = Choice(
                "Qual leitura o `intraday.context` sustenta para o movimento recente? " +
                "Use `recent_move`, `confluence`, `daily_volume` e `intraday_volume` como fatos, sem recalcular.",
                new Dictionary<string, string>
                {
                    ["continuar_alta"] = "O movimento de alta recente tem continuação provável.",
                    ["continuar_baixa"] = "O movimento de baixa recente tem continuação provável.",
                    ["lateralizar"] = "O movimento perdeu força e o preço tende a oscilar na região.",
                    ["reverter_para_baixa"] = "Após alta recente, há sinais de exaustão/absorção vendedora em zona diária de topo ou vale de baixo volume.",
                    ["reverter_para_alta"] = "Após baixa recente, há sinais de exaustão/absorção compradora em zona diária de fundo ou topo de baixo volume.",
                }),
            ["perda_de_forca"] = Score(
                "Quanta perda de força o movimento recente mostra no nível atual? " +
                "Use `recent_move`, `confluence` e `level_context`.",
                new[]
                {
                    "Sem perda de força",
                    "Perda moderada; sinais de exaustão",
                    "Perda forte; reversão provável",
                }),
            ["absorcao_no_nivel"] = Noul(
                "As bolhas filtradas no nível mostram absorção contra a direção do movimento recente? " +
                "Use `confluence.filtered_bubbles_at_zone`, `confluence.absorption_side` e `level_context`.",
                new Dictionary<string, string>
                {
                    ["true"] = "No topo, vendedoras absorvem compradoras; no fundo, compradoras absorvem vendedoras.",
                    ["false"] = "As bolhas seguem a favor do movimento ou não há cluster relevante.",
                }),
        };

        /// <summary>
        /// Interpreta o JSON cru da Jev: lê a confiança de cada indicador,
        /// agrega (Composite scoring) e calcula a proximidade técnica do
        /// gatilho. Puro, sem gate de operar/não-operar e sem I/O — testável
        /// sem chave.
        /// </summary>
        public static JevAnalysisResult ParseDecision(
            string symbol, string model, string answersJson,
            int inputTokens, int outputTokens, string state,
            JevWeights? weights = null)
        {
            var w = weights ?? JevWeights.Defaults();
            var indicators = new Dictionary<string, double>();
            ChoiceAnswer? choice = null;
            double? perdaScore = null, absorcao = null, evidencia = null;

            using (var doc = JsonDocument.Parse(answersJson,
                new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                var root = doc.RootElement;
                // Tolerância: aceita tanto o envelope {answers:{...}} quanto as
                // respostas diretas (útil em teste).
                var answers = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("answers", out var a) && a.ValueKind == JsonValueKind.Object
                        ? a : (JsonElement?)null;
                var node = answers ?? (root.ValueKind == JsonValueKind.Object ? root : (JsonElement?)null);
                if (node.HasValue)
                {
                    var n = node.Value;
                    if (n.TryGetProperty("leitura", out var le) &&
                        le.ValueKind == JsonValueKind.Object &&
                        le.TryGetProperty("type", out var lt) &&
                        lt.ValueKind == JsonValueKind.String &&
                        lt.GetString() == "choice")
                    {
                        choice = ReadChoice(le);
                        perdaScore = ReadScore(n, "perda_de_forca");
                        absorcao = ReadNoul(n, "absorcao_no_nivel");
                        evidencia = ReadNoul(n, "evidencia_suficiente");
                    }
                    else
                    {
                        // Formato antigo (25 noul) para auditorias/backfill.
                        foreach (var id in DirectionIndicators.Concat(OperabilityIndicators))
                        {
                            if (n.TryGetProperty(id, out var q) && q.ValueKind == JsonValueKind.Object &&
                                q.TryGetProperty("noul", out var v) && v.ValueKind == JsonValueKind.Number)
                                indicators[id] = v.GetDouble();
                        }
                    }
                }
            }

            var (prox, ticks, pref) = ProximityFromState(state, symbol, w);
            if (choice != null)
                return ParseLeitura(symbol, model, inputTokens, outputTokens, state,
                    choice, perdaScore, absorcao, evidencia, prox, ticks, pref);

            var scored = JevScoring.Evaluate(indicators, w, DirectionDimensions);
            var motive = string.IsNullOrWhiteSpace(scored.DirecaoMotivo)
                ? BuildDirectionMotive(scored, state)
                : scored.DirecaoMotivo;

            return new JevAnalysisResult
            {
                Symbol = symbol,
                Model = model,
                Direcao = scored.Direcao,
                DirecaoConfidence = scored.DirecaoConfidence,
                DirecaoNet = scored.DirecaoNet,
                DirecaoConsenso = scored.DirecaoConsenso,
                DirecaoCobertura = scored.DirecaoCobertura,
                DirecaoMotivo = motive,
                ConfiancaOperavel = scored.ConfiancaOperavel,
                Criteria = scored.Criteria,
                Proximidade = prox,
                ProximidadeTicks = ticks,
                ProximidadeRef = pref,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                StatePreview = state.Length <= 6000 ? state : state[..6000] + "…",
            };
        }

        private sealed record ChoiceAnswer(string Choice, double Confidence);

        private static ChoiceAnswer? ReadChoice(JsonElement element)
        {
            if (!element.TryGetProperty("choice", out var c) ||
                c.ValueKind != JsonValueKind.String)
                return null;
            var choice = c.GetString();
            if (string.IsNullOrWhiteSpace(choice)) return null;
            var confidence = element.TryGetProperty("confidence", out var cf) &&
                cf.ValueKind == JsonValueKind.Number
                    ? cf.GetDouble()
                    : 0.5;
            return new ChoiceAnswer(choice, Math.Clamp(confidence, 0, 1));
        }

        private static double? ReadScore(JsonElement answers, string id)
        {
            if (!answers.TryGetProperty(id, out var q) || q.ValueKind != JsonValueKind.Object)
                return null;
            if (!q.TryGetProperty("score", out var s) || s.ValueKind != JsonValueKind.Number)
                return null;
            return s.GetDouble();
        }

        private static double? ReadNoul(JsonElement answers, string id)
        {
            if (!answers.TryGetProperty(id, out var q) || q.ValueKind != JsonValueKind.Object)
                return null;
            if (!q.TryGetProperty("noul", out var n) || n.ValueKind != JsonValueKind.Number)
                return null;
            return Math.Clamp(n.GetDouble(), 0, 1);
        }

        private static JevAnalysisResult ParseLeitura(
            string symbol, string model, int inputTokens, int outputTokens,
            string state, ChoiceAnswer choice, double? perdaScore,
            double? absorcao, double? evidencia, double prox,
            double ticks, string pref)
        {
            var evid = Math.Clamp(evidencia ?? 1.0, 0, 1);
            var leitura = choice.Confidence < 0.35 || evid < 0.5
                ? "inconclusivo"
                : choice.Choice;
            var confidence = leitura == "inconclusivo"
                ? 0
                : Math.Clamp(choice.Confidence * evid, 0, 1);
            var perda = Math.Clamp((perdaScore ?? 0) / 2.0, 0, 1);
            var abs = Math.Clamp(absorcao ?? 0, 0, 1);
            var contexto = ContextScoreFromState(state);
            var (setupContexto, setupConfirmacao) = SetupFromState(state);
            var direcao = MapDirecao(leitura);
            var motivo = leitura switch
            {
                "inconclusivo" => "evidência insuficiente",
                "lateralizar" => "sem continuação clara",
                _ => "",
            };
            var criteria = new Dictionary<string, double>
            {
                ["leitura_conf"] = choice.Confidence,
                ["evidencia_suficiente"] = evid,
                ["perda_de_forca"] = perda,
                ["absorcao"] = abs,
                ["contexto"] = contexto,
            };

            return new JevAnalysisResult
            {
                Symbol = symbol,
                Model = model,
                Direcao = direcao,
                DirecaoConfidence = confidence,
                DirecaoNet = direcao == "comprar" ? confidence
                    : direcao == "vender" ? -confidence : 0,
                DirecaoConsenso = 1,
                DirecaoCobertura = 1,
                DirecaoMotivo = motivo,
                ConfiancaOperavel = contexto,
                Criteria = criteria,
                Proximidade = prox,
                ProximidadeTicks = ticks,
                ProximidadeRef = pref,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Leitura = leitura,
                LeituraConfidence = confidence,
                PerdaDeForca = perda,
                Absorcao = abs,
                Contexto = contexto,
                SetupContexto = setupContexto,
                SetupConfirmacao = setupConfirmacao,
                NivelChave = KeyLevelLabel(state),
                Evidencias = BuildEvidencias(state, leitura, perda, abs),
                StatePreview = state.Length <= 6000 ? state : state[..6000] + "…",
            };
        }

        private static string MapDirecao(string leitura) => leitura switch
        {
            "continuar_alta" or "reverter_para_alta" => "comprar",
            "continuar_baixa" or "reverter_para_baixa" => "vender",
            _ => "lateral",
        };

        private static (bool Contexto, bool Confirmacao) SetupFromState(string state)
        {
            try
            {
                using var doc = JsonDocument.Parse(state);
                if (!TryContext(doc.RootElement, out var context) ||
                    !context.TryGetProperty("confluence", out var confluence) ||
                    confluence.ValueKind != JsonValueKind.Object)
                    return (false, false);
                return (ReadBool(confluence, "setup_contexto"),
                    ReadBool(confluence, "setup_confirmacao"));
            }
            catch
            {
                return (false, false);
            }
        }

        private static double ContextScoreFromState(string state)
        {
            try
            {
                using var doc = JsonDocument.Parse(state);
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("intraday", out var intra) ||
                    intra.ValueKind != JsonValueKind.Object ||
                    !intra.TryGetProperty("context", out var context) ||
                    context.ValueKind != JsonValueKind.Object)
                    return 0.5;

                var efficiency = 0.5;
                var atrTicks = 0.0;
                var rangeTicks = 0.0;
                if (context.TryGetProperty("recent_move", out var move) &&
                    move.ValueKind == JsonValueKind.Object)
                {
                    efficiency = ReadDouble(move, "efficiency", efficiency);
                    atrTicks = ReadDouble(move, "atr_ticks", 0);
                    rangeTicks = ReadDouble(move, "range_ticks", 0);
                }

                var topDistance = 40.0;
                var valleyDistance = 40.0;
                if (intra.TryGetProperty("level_context", out var levels) &&
                    levels.ValueKind == JsonValueKind.Array)
                {
                    foreach (var level in levels.EnumerateArray())
                    {
                        if (level.ValueKind != JsonValueKind.Object) continue;
                        var kind = ReadString(level, "kind");
                        var side = ReadString(level, "side");
                        var distance = ReadDouble(level, "distance_ticks", 999);
                        if (kind == "topo" || side == "resistance")
                            topDistance = Math.Min(topDistance, distance);
                        if (kind == "vale" || side == "support")
                            valleyDistance = Math.Min(valleyDistance, distance);
                    }
                }

                var referenceDistance = 60.0;
                if (intra.TryGetProperty("nearest_pivot", out var nearest) &&
                    nearest.ValueKind == JsonValueKind.Object)
                    referenceDistance = ReadDouble(nearest, "distance_ticks", 60);

                var trend = Math.Clamp(efficiency, 0, 1);
                var amplitude = atrTicks > 0
                    ? Math.Clamp(rangeTicks / (3 * atrTicks), 0, 1) : 0;
                var space = Math.Clamp((topDistance + valleyDistance) / 80.0, 0, 1);
                var reference = Math.Clamp(1 - referenceDistance / 60.0, 0, 1);
                return Math.Clamp(
                    0.4 * trend + 0.3 * amplitude + 0.2 * space + 0.1 * reference,
                    0, 1);
            }
            catch
            {
                return 0.5;
            }
        }

        private static string KeyLevelLabel(string state)
        {
            try
            {
                using var doc = JsonDocument.Parse(state);
                if (TryContext(doc.RootElement, out var context) &&
                    context.TryGetProperty("confluence", out var confluence) &&
                    confluence.ValueKind == JsonValueKind.Object)
                {
                    var type = ReadString(confluence, "daily_zone_type");
                    var price = ReadDouble(confluence, "opposite_daily_zone_price", double.NaN);
                    var distance = ReadDouble(confluence, "opposite_daily_zone_distance_ticks", double.NaN);
                    if (!string.IsNullOrWhiteSpace(type) && !double.IsNaN(price))
                        return $"{type} diario {price:F0}" +
                            (double.IsNaN(distance) ? "" : $" ({distance:F0}t)");
                }
            }
            catch
            {
                // cai no pivô mais próximo
            }
            return NearestPivotLabel(state);
        }

        private static List<string> BuildEvidencias(
            string state, string leitura, double perda, double absorcao)
        {
            var evidence = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(state);
                if (TryContext(doc.RootElement, out var context))
                {
                    if (context.TryGetProperty("recent_move", out var move) &&
                        move.ValueKind == JsonValueKind.Object)
                    {
                        var direction = ReadString(move, "direction");
                        var strength = ReadString(move, "strength");
                        if (strength == "forte" && direction is "alta" or "baixa")
                            evidence.Add(
                                $"movimento previo de {direction} forte " +
                                $"({ReadDouble(move, "change_ticks", 0):F0}t em " +
                                $"{ReadDouble(move, "candles", 0):F0} candles)");
                    }

                    if (context.TryGetProperty("confluence", out var confluence) &&
                        confluence.ValueKind == JsonValueKind.Object)
                    {
                        var type = ReadString(confluence, "daily_zone_type");
                        var price = ReadDouble(confluence, "opposite_daily_zone_price", double.NaN);
                        var distance = ReadDouble(confluence, "opposite_daily_zone_distance_ticks", double.NaN);
                        if (!string.IsNullOrWhiteSpace(type) && !double.IsNaN(price))
                            evidence.Add($"{type} diario {price:F0}" +
                                (double.IsNaN(distance) ? "" : $" a {distance:F0}t"));

                        if (ReadBool(confluence, "low_volume_near_valley"))
                        {
                            var rank = ReadDouble(confluence, "low_volume_valley_rank", 0);
                            evidence.Add($"baixo volume perto de vale (rank {rank:F2})");
                        }

                        if (ReadBool(confluence, "filtered_bubbles_at_zone"))
                        {
                            var count = ReadDouble(confluence, "filtered_bubble_count", 0);
                            var absorption = ReadString(confluence, "absorption_side");
                            var text = $"bolhas filtradas no nivel ({count:F0})";
                            if (absorption is "buy" or "sell")
                                text += absorption == "buy"
                                    ? " com absorcao compradora"
                                    : " com absorcao vendedora";
                            evidence.Add(text);
                        }

                        if (ReadBool(confluence, "setup_confirmacao"))
                            evidence.Add("setup de confirmacao");
                        else if (ReadBool(confluence, "setup_contexto"))
                            evidence.Add("setup de contexto");
                    }
                }
            }
            catch
            {
                // evidência é best-effort
            }

            if (leitura == "inconclusivo")
                evidence.Add("evidencia insuficiente");
            else if (leitura == "lateralizar")
                evidence.Add("sem continuacao clara");
            if (perda >= 0.67)
                evidence.Add($"perda de forca alta ({perda:F2})");
            if (absorcao >= 0.5)
                evidence.Add($"absorcao ({absorcao:F2})");
            return evidence.Distinct().Take(4).ToList();
        }

        private static bool TryContext(JsonElement root, out JsonElement context)
        {
            context = default;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("intraday", out var intra) &&
                intra.ValueKind == JsonValueKind.Object &&
                intra.TryGetProperty("context", out context) &&
                context.ValueKind == JsonValueKind.Object;
        }

        private static string ReadString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";

        private static double ReadDouble(JsonElement element, string name, double fallback) =>
            element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : fallback;

        private static bool ReadBool(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            value.GetBoolean();

        /// <summary>
        /// Motivo legível de uma decisão direcional: nível mais próximo +
        /// 2 dimensões que mais contribuíram para o viés.
        /// </summary>
        private static string BuildDirectionMotive(JevScoringResult r, string state)
        {
            if (r.Direcao == "lateral" || r.Contributions.Count == 0)
                return r.DirecaoMotivo;

            var parts = new List<string>();
            var nearest = NearestPivotLabel(state);
            if (nearest.Length > 0) parts.Add(nearest);

            foreach (var (id, contribution) in r.Contributions
                         .OrderByDescending(kv => Math.Abs(kv.Value))
                         .Take(2))
            {
                var dim = DirectionDimensions.FirstOrDefault(d => d.Id == id);
                if (dim == null) continue;
                var label = DirectionLabels.TryGetValue(id, out var l) ? l : id;
                var side = contribution > 0 ? "alta" : "baixa";
                var key = contribution > 0 ? dim.BullId : dim.BearId;
                var pct = r.Criteria.TryGetValue(key, out var p)
                    ? $" {p * 100:F0}%"
                    : "";
                parts.Add($"{label} {side}{pct}");
            }
            return string.Join(" · ", parts);
        }

        private static string NearestPivotLabel(string state)
        {
            try
            {
                using var doc = JsonDocument.Parse(state);
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("intraday", out var intra) ||
                    intra.ValueKind != JsonValueKind.Object ||
                    !intra.TryGetProperty("nearest_pivot", out var np) ||
                    np.ValueKind != JsonValueKind.Object)
                    return "";
                var level = np.TryGetProperty("level", out var l) && l.ValueKind == JsonValueKind.String
                    ? l.GetString() : "";
                var value = np.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number
                    ? v.GetDouble() : double.NaN;
                var ticks = np.TryGetProperty("distance_ticks", out var t) && t.ValueKind == JsonValueKind.Number
                    ? t.GetDouble() : double.NaN;
                if (string.IsNullOrWhiteSpace(level) || double.IsNaN(value) || ticks > LevelContextMaxTicks)
                    return "";
                return $"{level} {value:F0} ({ticks:F0}t)";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Resumo compacto dos criteria para log/relatório (a decisão de operar
        /// é manual: aqui só se MOSTRA a confiança de cada indicador).
        /// </summary>
        public static string FormatCriteria(JevAnalysisResult r)
        {
            if (!string.IsNullOrWhiteSpace(r.Leitura))
            {
                var setup = r.SetupConfirmacao ? "confirmacao"
                    : r.SetupContexto ? "contexto" : "nenhum";
                var evidence = r.Evidencias.Count > 0
                    ? " | " + string.Join(" · ", r.Evidencias)
                    : "";
                return $"leitura={r.Leitura} conf={r.LeituraConfidence:F2} " +
                    $"perda={r.PerdaDeForca:F2} abs={r.Absorcao:F2} " +
                    $"ctx={r.Contexto:F2} setup={setup}{evidence}";
            }

            static string Pct(double v) => $"{v * 100:F0}%";
            var dirs = DirectionIndicators.Where(r.Criteria.ContainsKey)
                .Select(k => $"{k}={Pct(r.Criteria[k])}");
            var ops = OperabilityIndicators.Where(r.Criteria.ContainsKey)
                .Select(k => $"{k}={Pct(r.Criteria[k])}");
            var motivo = string.IsNullOrWhiteSpace(r.DirecaoMotivo)
                ? ""
                : $" motivo={r.DirecaoMotivo}";
            return $"vies={r.Direcao} conf={r.DirecaoConfidence:F2} "
                 + $"net={r.DirecaoNet:F2} cons={r.DirecaoConsenso:F2} cov={r.DirecaoCobertura:F2}"
                 + $"{motivo} op={r.ConfiancaOperavel:F2} "
                 + $"prox={r.Proximidade:F2}({r.ProximidadeRef} {r.ProximidadeTicks:F0}t) | "
                 + $"dir: {string.Join(" ", dirs)} | op: {string.Join(" ", ops)}";
        }

        /// <summary>
        /// Proximidade técnica a partir do state JSON: último candle intraday é
        /// o preço; referências com peso (pivô diário 2.0 > VWAP 1.5 > borda/
        /// topos). Puro, sem I/O.
        /// </summary>
        public static (double Proximidade, double Ticks, string Reference) ProximityFromState(
            string stateJson, string symbol, JevWeights? weights = null)
        {
            var w = weights ?? JevWeights.Defaults();
            var refs = new List<(string Name, double Value, double Weight)>();
            double? last = null;
            try
            {
                using var doc = JsonDocument.Parse(stateJson);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("intraday", out var intra) ||
                    intra.ValueKind != JsonValueKind.Object)
                    return (0, 0, "");

                if (intra.TryGetProperty("candles", out var cs) && cs.ValueKind == JsonValueKind.Array &&
                    cs.GetArrayLength() > 0)
                {
                    var c = cs[cs.GetArrayLength() - 1];
                    if (c.TryGetProperty("close", out var cl) && cl.ValueKind == JsonValueKind.Number)
                        last = cl.GetDouble();
                }
                if (last is null) return (0, 0, "");

                if (intra.TryGetProperty("vwap", out var vw) && vw.ValueKind == JsonValueKind.Array &&
                    vw.GetArrayLength() > 0)
                {
                    var v = vw[vw.GetArrayLength() - 1];
                    if (v.TryGetProperty("vwap", out var vv) && vv.ValueKind == JsonValueKind.Number)
                        refs.Add(("vwap", vv.GetDouble(), 1.5));
                }

                CollectPivots(intra, "pivo_intra", 1.0, refs);
                CollectStructures(intra, "estrutura_intra", 1.0, refs);
                CollectExtremes(intra, "extremo_intra", 1.0, refs);

                if (root.TryGetProperty("daily", out var daily) && daily.ValueKind == JsonValueKind.Object)
                {
                    CollectPivots(daily, "pivo_diario", 2.0, refs);
                    CollectStructures(daily, "estrutura_diaria", 1.25, refs);
                    CollectExtremes(daily, "extremo_diario", 1.25, refs);
                }
            }
            catch
            {
                return (0, 0, "");
            }

            return JevScoring.Proximity(last.Value, refs, Defaults.GetTickSize(symbol), w.ProximityMaxTicks);
        }

        private static void CollectPivots(JsonElement block, string name, double weight,
            List<(string Name, double Value, double Weight)> refs)
        {
            if (!block.TryGetProperty("pivots", out var pivots) || pivots.ValueKind != JsonValueKind.Array)
                return;
            foreach (var p in pivots.EnumerateArray())
                if (p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
                    refs.Add((name, v.GetDouble(), weight));
        }

        private static void CollectStructures(JsonElement block, string name, double weight,
            List<(string Name, double Value, double Weight)> refs)
        {
            if (!block.TryGetProperty("structures", out var structures) || structures.ValueKind != JsonValueKind.Array)
                return;
            foreach (var s in structures.EnumerateArray())
                foreach (var field in new[] { "up", "down", "up_aux", "down_aux" })
                    if (s.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Number)
                        refs.Add((name, v.GetDouble(), weight));
        }

        private static void CollectExtremes(JsonElement block, string name, double weight,
            List<(string Name, double Value, double Weight)> refs)
        {
            if (!block.TryGetProperty("tops_valleys", out var ex) || ex.ValueKind != JsonValueKind.Array)
                return;
            foreach (var e in ex.EnumerateArray())
                if (e.TryGetProperty("price", out var v) && v.ValueKind == JsonValueKind.Number)
                    refs.Add((name, v.GetDouble(), weight));
        }
    }
}
