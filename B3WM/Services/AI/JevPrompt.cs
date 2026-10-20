using System.Text.Json;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
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
            bool includeVolumeLevels = false)
        {
            var candles = snap.Get<List<BarStorageItem>>(SnapshotLayers.Candles) ?? new();
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

            var intraday = new JevIntradayDto
            {
                Scope = Range(intraScope, candles),
                Candles = candles.Select(ToCandle).ToList(),
                VolumeProfile = ToVolumeProfile(volume, includeVolumeLevels),
                Bubbles = bubbles.OrderBy(b => b.Date).Select(ToBubble).ToList(),
                Pivots = ToPivots(pivIntra, candles),
                TopsValleys = ToExtremes(extIntra),
                Structures = FilterIntradayStructures(structs, timeFrame, intraScope)
                    .Select(s => ToStructure(s, withTimeframe: true)).ToList(),
            };

            var daily = new JevDailyDto
            {
                Scope = Range(dailyScope, dailyCandles),
                Candles = dailyCandles.Select(ToCandle).ToList(),
                VolumeProfile = ToVolumeProfile(dailyVolume, includeVolumeLevels),
                Pivots = ToPivots(pivDaily, dailyCandles),
                TopsValleys = ToExtremes(extDaily),
                Structures = FilterDailyStructures(structs, dailyScope, dailyCandles)
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

        private static JevBubbleDto ToBubble(BubbleStorageItem b) => new()
        {
            Date = b.Date, Agent = b.Agent,
            Side = b.ActionType == Ticks2.ActionType.Buy ? "buy" : "sell",
            Amount = b.Amount, Price = b.Price,
        };

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

        private static List<JevExtremeDto> ToExtremes(ExtremeStorageItem? item) =>
            (item?.Extremes ?? new List<ExtremePoint>())
                .Where(e => e.Type is ExtremeType.Top or ExtremeType.Valley)
                .OrderBy(e => e.Position)
                .Select(e => new JevExtremeDto
                {
                    Type = e.Type == ExtremeType.Top ? "topo" : "vale",
                    Price = e.Position,
                })
                .ToList();

        /// <summary>
        /// Mede o tamanho (chars) de cada bloco do state JSON, para a auditoria
        /// (issue #23): mostra qual bloco domina tokens. Puro, sem I/O.
        /// </summary>
        public static Dictionary<string, int> MeasureBlocks(string symbol, int timeFrame, MarketSnapshot snap)
        {
            var state = BuildStateJson(symbol, timeFrame, snap);
            int Len(object v) => JsonSerializer.Serialize(v).Length;
            return new Dictionary<string, int>
            {
                ["intraday.candles"] = Len(state.Intraday.Candles),
                ["intraday.bubbles"] = Len(state.Intraday.Bubbles),
                ["intraday.volume"] = Len(state.Intraday.VolumeProfile),
                ["intraday.pivots"] = Len(state.Intraday.Pivots),
                ["intraday.extremes"] = Len(state.Intraday.TopsValleys),
                ["intraday.structures"] = Len(state.Intraday.Structures),
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

            var blocks = MeasureBlocks(symbol, timeFrame, snap);
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
                    BubblesSent = Je(snap.Has(SnapshotLayers.Bubbles) ? bubbles : null),
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
                    CandlesSent = candles.Count,
                    BubblesSent = snap.Has(SnapshotLayers.Bubbles) ? bubbles.Count : 0,
                    VolumeLevels = volume?.Volumes.Count ?? 0,
                    ExtremesIntraCount = extIntra?.Extremes.Count ?? 0,
                    ExtremesDailyCount = extDaily?.Extremes.Count ?? 0,
                    StructuresCount =
                        FilterIntradayStructures(structs, timeFrame,
                            snap.Get<DateWindow>(SnapshotLayers.ScopeIntraday)).Count +
                        FilterDailyStructures(structs,
                            snap.Get<DateWindow>(SnapshotLayers.ScopeDaily), dailyCandles).Count,
                    PivotIntraLevels = pivIntra?.Levels.Count ?? 0,
                    PivotDailyLevels = pivDaily?.Levels.Count ?? 0,
                    HasVwap = snap.Num(SnapshotLayers.Vwap) is not null,
                    StateChars = state.Length,
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

        /// <summary>IDs dos indicadores de direção (cada um afirma ALTA).</summary>
        public static readonly string[] DirectionIndicators =
        {
            "candles_alta", "acima_vwap", "estrutura_alta", "acima_poc",
            "delta_positivo", "bolhas_compra", "defende_suporte", "diario_alta",
        };

        /// <summary>IDs dos indicadores de operabilidade.</summary>
        public static readonly string[] OperabilityIndicators =
        {
            "sem_chop", "amplitude_suficiente", "sem_compressao_topovale",
            "referencia_clara", "sem_conflito_intra_diario",
        };

        private static object Noul(string instructions) => new Dictionary<string, object>
        {
            ["type"] = "noul",
            ["instructions"] = instructions,
        };

        /// <summary>
        /// Perguntas da Jev (Composite scoring): indicadores `noul` independentes
        /// que o CÓDIGO combina com pesos. Cada um é uma afirmação técnica e a
        /// resposta é a confiança (0..1). Sem pergunta de direção/gate.
        /// </summary>
        public static Dictionary<string, object> BuildQuestions() => new()
        {
            // Direção (afirmação de ALTA).
            ["candles_alta"] = Noul("Os candles/fundos recentes formam sequência ascendente (fundos e topos mais altos)?"),
            ["acima_vwap"] = Noul("O preço está acima do VWAP, ou rejeitou o VWAP de baixo para cima?"),
            ["estrutura_alta"] = Noul("A structure do timeframe está em ascensão (upgrade) e/ou o preço está na metade superior dela?"),
            ["acima_poc"] = Noul("O preço está acima do POC do volume profile intraday?"),
            ["delta_positivo"] = Noul("O delta do volume profile intraday é positivo (volume de compra > venda)?"),
            ["bolhas_compra"] = Noul("Há predomínio recente de bolhas de compra com delta positivo?"),
            ["defende_suporte"] = Noul("O preço está defendendo um suporte, vale ou borda inferior de structure?"),
            ["diario_alta"] = Noul("O bloco diário (candles/pivôs/topos-vales) sugere alta e confirma o intraday?"),
            // Operabilidade (qualidade do contexto).
            ["sem_chop"] = Noul("O preço NÃO está em lateralidade/chop apertado?"),
            ["amplitude_suficiente"] = Noul("A amplitude recente é suficiente para um gatilho com risco controlado?"),
            ["sem_compressao_topovale"] = Noul("O preço NÃO está espremido entre um topo e um vale muito próximos?"),
            ["referencia_clara"] = Noul("Existe uma referência clara e próxima (VWAP, structure ou pivô) para se orientar?"),
            ["sem_conflito_intra_diario"] = Noul("Intraday e diário NÃO se contradizem na direção?"),
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
                    foreach (var id in DirectionIndicators.Concat(OperabilityIndicators))
                    {
                        if (n.TryGetProperty(id, out var q) && q.ValueKind == JsonValueKind.Object &&
                            q.TryGetProperty("noul", out var v) && v.ValueKind == JsonValueKind.Number)
                            indicators[id] = v.GetDouble();
                    }
                }
            }

            var scored = JevScoring.Evaluate(indicators, w);
            var (prox, ticks, pref) = ProximityFromState(state, symbol, w);

            return new JevAnalysisResult
            {
                Symbol = symbol,
                Model = model,
                Direcao = scored.Direcao,
                DirecaoConfidence = scored.DirecaoConfidence,
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

        /// <summary>
        /// Resumo compacto dos criteria para log/relatório (a decisão de operar
        /// é manual: aqui só se MOSTRA a confiança de cada indicador).
        /// </summary>
        public static string FormatCriteria(JevAnalysisResult r)
        {
            static string Pct(double v) => $"{v * 100:F0}%";
            var dirs = DirectionIndicators.Where(r.Criteria.ContainsKey)
                .Select(k => $"{k}={Pct(r.Criteria[k])}");
            var ops = OperabilityIndicators.Where(r.Criteria.ContainsKey)
                .Select(k => $"{k}={Pct(r.Criteria[k])}");
            return $"vies={r.Direcao} conf={r.DirecaoConfidence:F2} op={r.ConfiancaOperavel:F2} "
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
