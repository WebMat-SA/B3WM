using System.Text;
using System.Text.Json;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Montagem da pergunta e leitura da resposta da Jev (contrato de prompt).
    /// Pertence à camada de análise (usado pela JevAnalysisStrategy e pelo
    /// endpoint avulso), NÃO ao transporte: o <see cref="JevService"/> só
    /// envia HTTP e devolve o JSON cru. Regra: nada aqui faz I/O.
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

        /// <summary>
        /// Monta o state WYSIWYG a partir do snapshot dinâmico: cada camada
        /// presente vira bloco; ausente/desligada vira linha informativa.
        /// Camada nova = nova chave em Layers + bloco aqui (sem bag tipada).
        /// </summary>
        public static string BuildState(string symbol, int timeFrame, MarketSnapshot snap)
        {
            var sb = new StringBuilder();
            var header = snap.Get<HeaderView>(SnapshotLayers.Header);
            AppendScreenHeader(sb, symbol, timeFrame, header);
            var candles = snap.Get<List<BarStorageItem>>(SnapshotLayers.Candles) ?? new();
            sb.AppendLine($"Candles (últimos {candles.Count}, OHLCV):");
            foreach (var c in candles)
                sb.AppendLine($"- {c.Date:HH:mm} O={c.Open} H={c.High} L={c.Low} C={c.Close} V={c.Volume}");

            if (!snap.Has(SnapshotLayers.Bubbles))
            {
                sb.AppendLine("Bolhas: desligadas na tela.");
            }
            else
            {
                var bubbles = snap.Get<List<BubbleStorageItem>>(SnapshotLayers.Bubbles) ?? new();
                var filt = header == null ? "" :
                    $" (filtro tela: thr base {header.ThresholdBubble}, {header.BubblesVisibleDay} de {header.BubblesTotalDay} no dia)";
                sb.AppendLine($"Bolhas — trades agressivos por agente (últimas {bubbles.Count}){filt}:");
                foreach (var b in bubbles)
                    sb.AppendLine($"- {b.Date:HH:mm:ss} agente={b.Agent} lado={b.ActionType} qtd={b.Amount} preco={b.Price}");
            }

            var volume = snap.Get<VolumeLevelStorageItem>(SnapshotLayers.Volume);
            var levels = volume?.Volumes ?? new List<VolumeLevel>();
            if (!snap.Has(SnapshotLayers.Volume))
            {
                sb.AppendLine("Volume profile: desligado na tela.");
            }
            else if (levels.Count == 0)
            {
                sb.AppendLine("Volume profile: sem dados.");
            }
            else
            {
                var poc = levels.OrderByDescending(l => l.Total).First();
                var top = levels.OrderByDescending(l => l.Total).Take(5).ToList();
                var buy = levels.Sum(l => l.BuyVolume);
                var sell = levels.Sum(l => l.SellVolume);
                sb.AppendLine($"Volume profile: POC={poc.Price} total={poc.Total} delta_total={buy - sell} (buy={buy} sell={sell}). Top níveis:");
                foreach (var l in top)
                    sb.AppendLine($"- preco={l.Price} total={l.Total} buy={l.BuyVolume} sell={l.SellVolume} delta={l.Delta}");
            }

            AppendPositionBlock(sb, snap.Get<PositionView>(SnapshotLayers.Position));
            AppendExtremesBlock(sb, "Topos e vales intraday",
                snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesIntra),
                snap.Has(SnapshotLayers.ExtremesIntra));
            AppendExtremesBlock(sb, "Topos e vales diários",
                snap.Get<ExtremeStorageItem>(SnapshotLayers.ExtremesDaily),
                snap.Has(SnapshotLayers.ExtremesDaily),
                snap.Get<DateWindow>(SnapshotLayers.DailyWindow));
            AppendStructuresBlock(sb, snap);
            AppendPivotBlock(sb, "Pivot intraday",
                snap.Get<PivotStorageItem>(SnapshotLayers.PivotIntra),
                snap.Has(SnapshotLayers.PivotIntra));
            AppendPivotBlock(sb, "Pivot diário",
                snap.Get<PivotStorageItem>(SnapshotLayers.PivotDaily),
                snap.Has(SnapshotLayers.PivotDaily));
            AppendVwapLine(sb, snap);
            return sb.ToString();
        }

        private static void AppendScreenHeader(StringBuilder sb, string symbol, int timeFrame, HeaderView? header)
        {
            sb.AppendLine($"Ativo: {symbol} Timeframe: {timeFrame}min. Análise de microestrutura para decisão comprar/vender/manter.");
            if (header == null) return;
            var tfs = header.VisibleTimeFrames.Count > 0
                ? string.Join(",", header.VisibleTimeFrames)
                : timeFrame.ToString();
            sb.AppendLine($"Tela: dia={header.DisplayDate:yyyy-MM-dd} modo={header.Mode} último_preço={header.LastPrice?.ToString("F1") ?? "?"} " +
                $" structures_tf=[{tfs}] minDistance={header.MinDistance} thresholdBubble={header.ThresholdBubble}" +
                $" vwap={(header.VwapVisible ? "on" : "off")} painel1D={(header.PanelOpen ? "aberto" : "fechado")}" +
                $" trading(hist={(header.TradingHistoryVisible ? "on" : "off")},pos={(header.PositionVisible ? "on" : "off")},ordens={(header.OpenOrdersVisible ? "on" : "off")}).");
        }

        /// <summary>
        /// Situação da posição aberta (ou flat) para a IA decidir entrada e saída.
        /// </summary>
        public static void AppendPositionBlock(StringBuilder sb, PositionView? pos)
        {
            if (pos == null)
            {
                sb.AppendLine("Posição: flat (sem posição aberta).");
                return;
            }
            var side = pos.Side == "comprar" ? "comprado" : "vendido";
            var sign = pos.UnrealizedPts >= 0 ? "+" : "";
            sb.AppendLine($"Posição: {side} @{pos.Entry:F0} desde {pos.EntryTime:HH:mm} ({pos.Candles} candles, parcial {sign}{pos.UnrealizedPts:F0} pts). Responda encerrar_posicao=1 se devemos sair agora.");
        }

        /// <summary>
        /// Topos/vales confirmados (Top/Valley) mais relevantes por
        /// prominência; Indeterminate entra só na contagem.
        /// layerOn=false (desligado na tela) vs item null (sem dados).
        /// </summary>
        public static void AppendExtremesBlock(StringBuilder sb, string title,
            ExtremeStorageItem? item, bool layerOn, DateWindow? window = null)
        {
            if (!layerOn)
            {
                sb.AppendLine($"{title}: desligados na tela.");
                return;
            }
            var extremes = item?.Extremes ?? new List<ExtremePoint>();
            var tops = extremes.Count(e => e.Type == ExtremeType.Top);
            var valleys = extremes.Count(e => e.Type == ExtremeType.Valley);
            var indet = extremes.Count - tops - valleys;
            if (tops + valleys == 0)
            {
                sb.AppendLine($"{title}: sem dados.");
                return;
            }
            var period = item?.PeriodFrom != null || item?.PeriodTo != null
                ? $" período {item.PeriodFrom:dd/MM}-{item.PeriodTo:dd/MM}"
                : window != null ? $" período {window.From:dd/MM}-{window.To:dd/MM}" : string.Empty;
            sb.AppendLine($"{title} ({tops} topos, {valleys} vales, {indet} indet.{period}):");
            foreach (var e in extremes
                         .Where(e => e.Type is ExtremeType.Top or ExtremeType.Valley)
                         .OrderByDescending(e => e.Prominence)
                         .Take(15))
            {
                var kind = e.Type == ExtremeType.Top ? "topo" : "vale";
                sb.AppendLine($"- {kind} preco={e.Position} vol={e.Value:F0} prom={e.Prominence:F0} conf={e.Confidence:F2}");
            }
        }

        /// <summary>
        /// Bordas de estrutura (Up/Down/Aux) mais próximas do último preço
        /// por timeframe visível — as que importam visualmente como
        /// suporte/resistência. Sem preço de referência: últimas por data.
        /// </summary>
        public static void AppendStructuresBlock(StringBuilder sb, MarketSnapshot snap, int maxPerTimeFrame = 8)
        {
            var header = snap.Get<HeaderView>(SnapshotLayers.Header);
            if (!snap.Has(SnapshotLayers.Structures))
            {
                sb.AppendLine("Structures: desligadas na tela.");
                return;
            }
            var all = snap.Get<List<StructureStorageItem>>(SnapshotLayers.Structures) ?? new();
            if (all.Count == 0)
            {
                sb.AppendLine("Structures: sem dados.");
                return;
            }
            var tfs = header?.VisibleTimeFrames is { Count: > 0 } vt
                ? vt : all.Select(s => s.TimeFrame).Distinct().OrderBy(t => t).ToList();
            var includeAux = snap.Value(SnapshotLayers.StructureAuxVisibleFlag, true);
            var lastPrice = header?.LastPrice;
            var refDay = header?.DisplayDate.Date ?? DateTime.Today;
            sb.AppendLine($"Structures LTA/LTB (bordas mais próximas do preço, minDistance={header?.MinDistance ?? 0}):");
            foreach (var tf in tfs)
            {
                var rows = FlattenBorders(all.Where(s => s.TimeFrame == tf), includeAux);
                if (rows.Count == 0)
                {
                    sb.AppendLine($"- tf {tf}: sem dados.");
                    continue;
                }
                IEnumerable<(string Label, double Price, DateTime Date)> picked = lastPrice is double lp
                    ? rows.OrderBy(r => Math.Abs(r.Price - lp)).Take(Math.Max(1, maxPerTimeFrame))
                    : rows.OrderByDescending(r => r.Date).Take(Math.Max(1, maxPerTimeFrame));
                foreach (var r in picked.OrderBy(r => r.Price))
                {
                    var side = lastPrice is double lp2
                        ? (r.Price < lp2 ? "suporte" : "resistencia")
                        : "nível";
                    var dist = lastPrice is double lp3
                        ? $" dist={(r.Price - lp3):F1}" : string.Empty;
                    // Structures de dias anteriores: mostra o dia (só HH:mm
                    // quando é o dia exibido), senão a IA confunde com hoje.
                    var when = r.Date.Date == refDay
                        ? r.Date.ToString("HH:mm") : r.Date.ToString("dd/MM HH:mm");
                    sb.AppendLine($"- [tf {tf}] {side} preco={r.Price} ({r.Label} {when}){dist}");
                }
            }
        }

        private static List<(string Label, double Price, DateTime Date)> FlattenBorders(
            IEnumerable<StructureStorageItem> items, bool includeAux)
        {
            var rows = new List<(string, double, DateTime)>();
            foreach (var s in items)
            {
                rows.Add(("Up", s.UpBorder, s.Date));
                rows.Add(("Down", s.DownBorder, s.Date));
                if (!includeAux) continue;
                if (s.UpAuxBorder != 0) rows.Add(("UpAux", s.UpAuxBorder, s.Date));
                if (s.DownAuxBorder != 0) rows.Add(("DownAux", s.DownAuxBorder, s.Date));
            }
            return rows;
        }

        public static void AppendPivotBlock(StringBuilder sb, string title, PivotStorageItem? pivot, bool layerOn)
        {
            if (!layerOn)
            {
                sb.AppendLine($"{title}: desligado na tela.");
                return;
            }
            if (pivot == null || pivot.Levels.Count == 0)
            {
                sb.AppendLine($"{title}: sem dados.");
                return;
            }
            var lv = string.Join(" ", pivot.Levels.Select(l => $"{l.Key}={l.Value}"));
            sb.AppendLine($"{title} ({pivot.Source}, sessão {pivot.Date:dd/MM}): {lv} (HLC fonte: H={pivot.High} L={pivot.Low} C={pivot.Close})");
        }

        public static void AppendVwapLine(StringBuilder sb, MarketSnapshot snap)
        {
            var vwap = snap.Num(SnapshotLayers.Vwap);
            var lp = snap.Get<HeaderView>(SnapshotLayers.Header)?.LastPrice;
            if (vwap is not double v || lp is not double price || price == 0)
            {
                sb.AppendLine("VWAP: off ou sem dados.");
                return;
            }
            var pct = (price - v) / price * 100;
            var pos = price >= v ? "acima" : "abaixo";
            sb.AppendLine($"VWAP do dia: vwap={v:F1} preço {pos} ({pct:+0.00;-0.00}%).");
        }

        /// <summary>
        /// VWAP cumulativa do dia de referência (typical HLC/3 ponderado por
        /// volume), igual ao cálculo do gráfico (chart_data.dart). Retorna o
        /// último valor ou null sem barras com volume.
        /// </summary>
        public static double? ComputeVwap(IEnumerable<BarStorageItem> candles, DateTime referenceDay)
        {
            var day = referenceDay.Date;
            double totalVol = 0, totalPv = 0, last = double.NaN;
            foreach (var c in candles.Where(c => c.Date.Date == day).OrderBy(c => c.Date))
            {
                if (c.Volume <= 0) continue;
                totalVol += c.Volume;
                totalPv += (c.High + c.Low + c.Close) / 3 * c.Volume;
                if (totalVol > 0) last = totalPv / totalVol;
            }
            return double.IsNaN(last) ? null : last;
        }

        public static object BuildQuestions() => new Dictionary<string, object>
        {
            ["direcao"] = new
            {
                type = "choice",
                instructions = "Dado o state de candles+bolhas+volume profile+topos/vales+structures+pivots+vwap, qual ação tomar?",
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
            ["encerrar_posicao"] = new
            {
                type = "noul",
                instructions = "Com a posição atual descrita no bloco Posição (se flat, responda 0): o contexto exige encerrar a posição agora (reversão, alvo atingido, perda de sustentação)?",
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
