using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Screen;
using B3WM.Shared.Models;

namespace B3WM.Services.Strategies
{
    /// <summary>
    /// Análise da IA como strategy: injeta JevService, monta o snapshot
    /// WYSIWYG a partir do spec cru da sessão e converte a resposta em
    /// StrategyDecision. Gatilho: fechamento do candle do TF da sessão que
    /// teve pelo menos uma bolha; a Jev recebe TODAS as bolhas do candle
    /// (inclusive a que seria "primeira visível"). Nunca dispara no
    /// aparecimento da bolha no candle vigente.
    /// </summary>
    public sealed class JevAnalysisStrategy : StrategyBase
    {
        private readonly JevService _jev;
        private readonly ScreenStateBuilder _builder;
        private readonly JevAuditStore _audit;
        private readonly B3WM.Services.DataKeeperBase _keeper;

        public JevAnalysisStrategy(
            ILogger<JevAnalysisStrategy> logger,
            JevService jev,
            ScreenStateBuilder builder,
            JevAuditStore audit,
            B3WM.Services.DataKeeperBase keeper) : base(logger)
        {
            _jev = jev;
            _builder = builder;
            _audit = audit;
            _keeper = keeper;
        }

        public override string Name => "JevAnalysis";
        public override string Description => "Análise Jev/TypeSafe do contexto (bolhas, volume, candles, topos/vales, pivots)";

        public override string DetailsMd =>
            "## JevAnalysis\n" +
            "Análise Jev/TypeSafe do contexto de mercado (bolhas, volume, candles, topos/vales, pivots).\n" +
            "\n" +
            "- **Gatilho:** fechamento do candle do TF da sessão com pelo menos uma bolha; a Jev recebe todas as bolhas ocorridas no candle (não dispara na abertura/1ª bolha do candle vigente).\n" +
            "- **TF:** o TF vigente no PLAY (filtros travados na sessão; execução no open do próprio TF).\n" +
            "- **Parâmetros fixos no código:** `LookbackCandles=60`, `LookbackBubbles=30`.\n" +
            "- **Saída INFORMATIVA (Composite scoring):** 10 dimensões de direção com perguntas espelhadas alta/baixa (20 perguntas) + 5 de operabilidade; viés `comprar|vender|lateral`, proximidade técnica e contexto de níveis (pivô/vale + cluster de bolhas). **Não** decide operar/não-operar — a decisão é manual; pesos/thresholds em `JevWeights` (sobrescrevíveis por `Data/jev_weights.json`).\n" +
            "- **Foto da tela:** o PLAY congela `SymbolConfig` (filtros de bolhas, estrutura, visibilidades); " +
            "a avaliação reconstrói o snapshot a partir dessa foto.\n" +
            "- **Riscos/limitações:** depende da IA (latência/cota); log-only (sem envio real); " +
            "para mudar filtros é preciso `STOP`, editar e `PLAY` de novo.";

        /// <summary>
        /// Dispara no fechamento do candle do TF quando houve bolha nele.
        /// O runner entrega a lista completa de bolhas do candle no evento.
        /// </summary>
        public override bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx) =>
            TriggerChecks.IsCandleCloseWithBubbles(ev, ctx.TimeFrame);

        /// <summary>Config 100% no código (nada no tab).</summary>
        private const int LookbackCandles = 60;
        private const int LookbackBubbles = 30;

        public override async Task<StrategyDecision?> EvaluateAsync(
            StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default)
        {
            // TF da sessão (não do evento): bubble não carrega TF.
            var tf = ctx.TimeFrame;
            // Candle fechado: avalia no fechamento (open + TF) e corta o
            // snapshot nele, para incluir o próprio candle e as bolhas dele.
            var candle = ev as CandleClosed;
            var at = candle != null
                ? candle.Bar.Date.AddMinutes(candle.Bar.TimeFrame)
                : ev.At;
            var asOf = candle != null || ev.At.Date < DateTime.Today ? at : (DateTime?)null;

            using var doc = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(ctx.ScreenSpecJson) ? "{}" : ctx.ScreenSpecJson);
            var snap = await _builder.BuildAsync(new ScreenStateRequest
            {
                Symbol = ctx.Symbol,
                TimeFrame = tf,
                DisplayDate = at.Date,
                AsOf = asOf,
                VisibleTimeFrames = new() { tf },
                Spec = doc.RootElement.Clone(),
                // JEV: escopo resolvido no backend + diário sempre.
                IncludeDaily = true,
                ResolveScope = true,
                LookbackCandles = LookbackCandles,
                LookbackBubbles = LookbackBubbles,
            }, ct);
            ctx.Snapshot = snap;
            // Garante que TODAS as bolhas do candle (e a do trigger manual)
            // entrem no state, mesmo abaixo do threshold da tela ou fora do
            // recorte do último candle fechado usado pelo builder.
            MergeTriggerBubbles(snap, ev);
            MergeLevelClusterBubbles(snap, at);
            snap.Raw["triggerBubbleKeys"] = TriggerKeysFor(ev);

            var state = JevPrompt.BuildStateJson(ctx.Symbol, tf, snap);
            var stateJson = JsonSerializer.Serialize(state);
            var questions = JevPrompt.BuildQuestions();
            var request = _jev.BuildRequest(state, questions);
            var weights = await JevWeights.LoadAsync(_keeper);

            // Auditoria: 1 arquivo por chamada em Data/JEV, SEMPRE — inclusive
            // quando a chamada falha (HTTP/timeout/sem chave). O id é gerado
            // antes para o mesmo arquivo cobrir sucesso e erro.
            var auditId = B3WM.Shared.Models.AI.JevAuditFiles.NewId();
            JevService.JevResponse? resp = null;
            B3WM.Shared.Models.AI.JevAnalysisResult? res = null;
            try
            {
                resp = await _jev.AskAsync(request, ct);
                res = JevPrompt.ParseDecision(ctx.Symbol, resp.Model, resp.Body,
                    resp.InputTokens, resp.OutputTokens, stateJson, weights);
            }
            catch (Exception ex)
            {
                await TryWriteAuditAsync(auditId, snap, stateJson, request.Json,
                    ev, ctx, tf, resp, null, ex, ct);
                throw;
            }

            var auditFile = await TryWriteAuditAsync(auditId, snap, stateJson,
                request.Json, ev, ctx, tf, resp, res, null, ct);
            return new StrategyDecision
            {
                Side = res!.Direcao,
                Confidence = res.DirecaoConfidence,
                Criteria = res.Criteria,
                Reason = JevPrompt.FormatCriteria(res),
                DirecaoNet = res.DirecaoNet,
                DirecaoConsenso = res.DirecaoConsenso,
                DirecaoCobertura = res.DirecaoCobertura,
                DirecaoMotivo = res.DirecaoMotivo,
                StateChars = res.StatePreview.Length,
                AuditFile = auditFile,
            };
        }

        /// <summary>
        /// Une as bolhas do gatilho à camada já montada pelo builder. Assim a
        /// Jev vê todas as bolhas do candle (inclusive abaixo do threshold da
        /// tela) e a bolha do trigger manual, sem alterar o pool de auditoria.
        /// </summary>
        private static void MergeTriggerBubbles(MarketSnapshot snap, StrategyEvent ev)
        {
            var extra = ev switch
            {
                CandleClosed c => c.Bubbles,
                BubbleAppeared b => new List<BubbleStorageItem> { b.Item },
                _ => new List<BubbleStorageItem>(),
            };
            if (extra.Count == 0) return;

            var current = snap.Get<List<BubbleStorageItem>>(SnapshotLayers.Bubbles)
                ?? new List<BubbleStorageItem>();
            snap.Layers[SnapshotLayers.Bubbles] = MergeBubbles(current, extra);
        }

        /// <summary>
        /// Inclui no state as bolhas do pool bruto que estão dentro do raio
        /// dos pivôs/vale/topo relevantes, mesmo abaixo do threshold da tela.
        /// </summary>
        private static void MergeLevelClusterBubbles(MarketSnapshot snap, DateTime asOf)
        {
            var extra = JevPrompt.SelectLevelClusterBubbles(snap, asOf);
            if (extra.Count == 0) return;
            var current = snap.Get<List<BubbleStorageItem>>(SnapshotLayers.Bubbles)
                ?? new List<BubbleStorageItem>();
            snap.Layers[SnapshotLayers.Bubbles] = MergeBubbles(current, extra);
        }

        /// <summary>Chaves das bolhas que pertencem ao gatilho avaliado.</summary>
        private static HashSet<string> TriggerKeysFor(StrategyEvent ev) => ev switch
        {
            CandleClosed c => c.Bubbles.Select(JevPrompt.BubbleKey).ToHashSet(),
            BubbleAppeared b => new HashSet<string> { JevPrompt.BubbleKey(b.Item) },
            _ => new HashSet<string>(),
        };

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
        /// Monta e grava a auditoria (best-effort). Nunca lança: falha de
        /// auditoria vira warning e não mascara o erro original da chamada.
        /// </summary>
        private async Task<string> TryWriteAuditAsync(
            string auditId,
            MarketSnapshot snap,
            string stateJson,
            string payload,
            StrategyEvent ev,
            StrategyContext ctx,
            int tf,
            JevService.JevResponse? resp,
            B3WM.Shared.Models.AI.JevAnalysisResult? res,
            Exception? failure,
            CancellationToken ct)
        {
            try
            {
                var header = snap.Get<HeaderView>(SnapshotLayers.Header);
                var http = resp?.HttpStatus
                    ?? (failure as JevHttpException)?.HttpStatus ?? 0;
                var body = resp?.Body
                    ?? (failure as JevHttpException)?.Body ?? "";
                var attempts = resp?.Attempts
                    ?? (failure as JevHttpException)?.Attempts
                    ?? (failure as JevTimeoutException)?.Attempts ?? 1;
                var latency = resp?.LatencyMs
                    ?? (failure as JevHttpException)?.LatencyMs
                    ?? (failure as JevTimeoutException)?.LatencyMs ?? 0;

                var record = JevPrompt.BuildAudit(
                    auditId,
                    DateTime.Now, StrategyRunner.EvaluationTime(ev), ev.GetType().Name,
                    ctx.Symbol, tf,
                    header?.DisplayDate ?? DateTime.Today,
                    "strategy", resp?.Model ?? _jev.Model, ctx.SnapshotHash ?? "",
                    LookbackCandles, LookbackBubbles,
                    snap, stateJson,
                    payload,
                    http, latency, attempts, body, res,
                    JevPrompt.ParseErrorType(body), failure?.Message ?? "");
                var auditFile = await _audit.TryWriteAsync(record, ct);
                if (res != null) res.AuditFile = auditFile;
                return auditFile;
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "[jev-audit] falha ao montar auditoria {Symbol}", ctx.Symbol);
                return "";
            }
        }
    }
}
