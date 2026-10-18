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
        public static JevStateDto BuildStateJson(string symbol, int timeFrame, MarketSnapshot snap)
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

            var intraday = new JevIntradayDto
            {
                Scope = Range(snap.Get<DateWindow>(SnapshotLayers.ScopeIntraday), candles),
                Candles = candles.Select(ToCandle).ToList(),
                VolumeProfile = ToVolumeProfile(volume),
                Bubbles = bubbles.OrderBy(b => b.Date).Select(ToBubble).ToList(),
                Vwap = candles.Where(c => c.VwapPrice.HasValue)
                    .Select(c => new JevVwapDto { Minute = c.Date.ToString("HH:mm"), Vwap = c.VwapPrice!.Value })
                    .ToList(),
                Pivots = ToPivots(pivIntra, candles),
                TopsValleys = ToExtremes(extIntra),
                Structures = structs.Where(s => s.TimeFrame != 1440)
                    .OrderBy(s => s.Date).Select(s => ToStructure(s, withTimeframe: true)).ToList(),
            };

            var dailyStructs = structs.Where(s => s.TimeFrame == 1440);
            var daily = new JevDailyDto
            {
                Scope = Range(snap.Get<DateWindow>(SnapshotLayers.ScopeDaily), dailyCandles),
                Candles = dailyCandles.Select(ToCandle).ToList(),
                VolumeProfile = ToVolumeProfile(dailyVolume),
                Pivots = ToPivots(pivDaily, dailyCandles),
                TopsValleys = ToExtremes(extDaily),
                Structures = dailyStructs.OrderBy(s => s.Date).Select(s => ToStructure(s, withTimeframe: false)).ToList(),
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

        /// <summary>POC + delta total + todos os níveis do escopo (sem teto).</summary>
        private static JevVolumeProfileDto? ToVolumeProfile(VolumeLevelStorageItem? volume)
        {
            var levels = volume?.Volumes;
            if (levels == null || levels.Count == 0) return null;
            var poc = levels.OrderByDescending(l => l.Total).First();
            return new JevVolumeProfileDto
            {
                Poc = poc.Price,
                DeltaTotal = levels.Sum(l => l.BuyVolume) - levels.Sum(l => l.SellVolume),
                Levels = levels.OrderBy(l => l.Price).Select(l => new JevVolumeLevelDto
                {
                    Price = l.Price, Total = l.Total,
                    Buy = l.BuyVolume, Sell = l.SellVolume, Delta = l.Delta,
                }).ToList(),
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
                ["intraday.vwap"] = Len(state.Intraday.Vwap),
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
        /// o que filtrou, pergunta, chamada, resposta). Puro, sem I/O e sem
        /// chave — a gravação em Data/JEV vive no JevAuditStore.
        /// <paramref name="state"/> é o state JSON já serializado.
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
            double threshold,
            int lookbackCandles,
            int lookbackBubbles,
            MarketSnapshot snap,
            string state,
            string questionsJson,
            int httpStatus,
            long latencyMs,
            int attempts,
            string bodyRaw,
            JevAnalysisResult parsed)
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
                Threshold = threshold,
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
                    StructuresCount = structs?.Count ?? 0,
                    PivotIntraLevels = pivIntra?.Levels.Count ?? 0,
                    PivotDailyLevels = pivDaily?.Levels.Count ?? 0,
                    HasVwap = snap.Num(SnapshotLayers.Vwap) is not null,
                    StateChars = state.Length,
                    StateBlocks = blocks,
                },
                QuestionState = state,
                QuestionQuestionsJson = string.IsNullOrWhiteSpace(questionsJson) ? "{}" : questionsJson,
                Call = new JevAuditCall
                {
                    HttpStatus = httpStatus,
                    LatencyMs = latencyMs,
                    Attempts = attempts,
                },
                Answer = new JevAuditAnswer
                {
                    BodyRaw = bodyRaw ?? "",
                    Direcao = parsed.Direcao,
                    DirecaoConfidence = parsed.DirecaoConfidence,
                    DirecaoProbabilities = new Dictionary<string, double>(parsed.DirecaoProbabilities),
                    ForcaTendencia = parsed.ForcaTendencia,
                    ForcaConfidence = parsed.ForcaConfidence,
                    ConfiancaOperavel = parsed.ConfiancaOperavel,
                    EncerrarPosicao = parsed.EncerrarPosicao,
                    ShouldTrade = parsed.ShouldTrade,
                    InputTokens = parsed.InputTokens,
                    OutputTokens = parsed.OutputTokens,
                },
            };
        }

        public static object BuildQuestions() => new Dictionary<string, object>
        {
            ["direcao"] = new
            {
                type = "choice",
                instructions = "Dado o state (bloco intraday: candles+volume_profile+bolhas+vwap+pivots+topos/vales+structures; bloco diario: candles+volume_profile+pivots+topos/vales+structures), qual ação tomar?",
                criteria = new Dictionary<string, string>
                {
                    ["comprar"] = "Pressão compradora: candles de alta, bolhas de compra, delta positivo, preço acima do POC/VWAP com defesa em vale ou suporte de structure, alvo em topo/resistência ou R1",
                    ["vender"] = "Pressão vendedora: candles de baixa, bolhas de venda, delta negativo, preço abaixo do POC/VWAP com defesa em topo ou resistência de structure, alvo em vale/suporte ou S1",
                    ["manter"] = "Sem edge claro: lateral, preço espremido entre topo/vale próximos, conflito entre candles/bolhas/delta ou baixa confiança",
                },
            },
            ["forca_tendencia"] = new
            {
                type = "score",
                instructions = "Força da tendência no contexto atual",
                criteria = new[] { "fraca/lateral, sem direção", "moderada, direção emergente", "forte/direcional, continuação provável" },
            },
            ["confianca_operavel"] = new
            {
                type = "noul",
                instructions = "O contexto atual é operável (tendência legível, sem ruído excessivo ou conflito de sinais)?",
            },
        };

        /// <summary>
        /// Interpreta o JSON cru da Jev no resultado estruturado (gate local
        /// incluído). Puro, sem I/O — testável sem chave.
        /// </summary>
        public static JevAnalysisResult ParseDecision(
            string symbol, string model, string answersJson,
            int inputTokens, int outputTokens, double threshold, string state)
        {
            using var doc = JsonDocument.Parse(answersJson,
                new JsonDocumentOptions { AllowTrailingCommas = true });
            var root = doc.RootElement;
            var answers = root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("answers", out var a) && a.ValueKind == JsonValueKind.Object
                    ? a : (JsonElement?)null;
            // Tolerância: aceita tanto o envelope {answers:{...}} quanto as
            // respostas diretas (útil em teste).
            var node = answers ?? (root.ValueKind == JsonValueKind.Object ? root : (JsonElement?)null);

            var direcao = "manter";
            var direcaoConf = 0d;
            var probs = new Dictionary<string, double>();
            var forca = 0d;
            var forcaConf = 0d;
            var operavel = 0d;
            var encerrar = 0d;

            if (node.HasValue)
            {
                var n = node.Value;
                if (n.TryGetProperty("direcao", out var d) && d.ValueKind == JsonValueKind.Object)
                {
                    if (d.TryGetProperty("choice", out var ch) && ch.ValueKind == JsonValueKind.String)
                        direcao = ch.GetString()?.ToLowerInvariant() ?? "manter";
                    if (d.TryGetProperty("confidence", out var cf) && cf.ValueKind == JsonValueKind.Number)
                        direcaoConf = cf.GetDouble();
                    if (d.TryGetProperty("probabilities", out var pr) && pr.ValueKind == JsonValueKind.Object)
                        foreach (var p in pr.EnumerateObject())
                            if (p.Value.ValueKind == JsonValueKind.Number)
                                probs[p.Name.ToLowerInvariant()] = p.Value.GetDouble();
                }
                if (n.TryGetProperty("forca_tendencia", out var f) && f.ValueKind == JsonValueKind.Object)
                {
                    if (f.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number)
                        forca = s.GetDouble();
                    if (f.TryGetProperty("confidence", out var fc) && fc.ValueKind == JsonValueKind.Number)
                        forcaConf = fc.GetDouble();
                }
                if (n.TryGetProperty("confianca_operavel", out var no) && no.ValueKind == JsonValueKind.Object)
                {
                    if (no.TryGetProperty("noul", out var nv) && nv.ValueKind == JsonValueKind.Number)
                        operavel = nv.GetDouble();
                }
                if (n.TryGetProperty("encerrar_posicao", out var e) && e.ValueKind == JsonValueKind.Object)
                {
                    if (e.TryGetProperty("noul", out var ev) && ev.ValueKind == JsonValueKind.Number)
                        encerrar = ev.GetDouble();
                }
            }

            return new JevAnalysisResult
            {
                Symbol = symbol,
                Model = model,
                Direcao = direcao,
                DirecaoConfidence = direcaoConf,
                DirecaoProbabilities = probs,
                ForcaTendencia = forca,
                ForcaConfidence = forcaConf,
                ConfiancaOperavel = operavel,
                ShouldTrade = JevDecision.ApplyGate(direcao, direcaoConf, operavel, threshold),
                EncerrarPosicao = encerrar,
                Threshold = threshold,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                StatePreview = state.Length <= 6000 ? state : state[..6000] + "…",
            };
        }
    }
}
