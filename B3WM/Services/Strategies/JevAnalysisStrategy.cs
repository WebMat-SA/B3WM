using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Screen;

namespace B3WM.Services.Strategies
{
    /// <summary>
    /// Análise da IA como strategy: injeta JevService, monta o snapshot
    /// WYSIWYG a partir do spec cru da sessão e converte a resposta em
    /// StrategyDecision. Gatilho: primeira bubble visível por candle do TF
    /// da sessão (filtro da tela congelado no PLAY; ritmo via bucket da hora
    /// da bubble no runner). Sem trigger em fechamento de candle.
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
            "- **Gatilho:** primeira bubble visível por candle do TF da sessão (filtro da tela congelado no PLAY; bucket pela hora da bubble).\n" +
            "- **TF:** o TF vigente no PLAY (filtros travados na sessão; execução no open do próprio TF).\n" +
            "- **Parâmetros fixos no código:** `LookbackCandles=60`, `LookbackBubbles=30`.\n" +
            "- **Saída INFORMATIVA (Composite scoring):** confiança por criteria (8 de direção + 5 de operabilidade), viés e proximidade técnica. **Não** decide operar/não-operar — a decisão é manual; pesos fixos em `JevWeights` (sobrescrevíveis por `Data/jev_weights.json`).\n" +
            "- **Foto da tela:** o PLAY congela `SymbolConfig` (filtros de bolhas, estrutura, visibilidades); " +
            "a avaliação reconstrói o snapshot a partir dessa foto.\n" +
            "- **Riscos/limitações:** depende da IA (latência/cota); log-only (sem envio real); " +
            "para mudar filtros é preciso `STOP`, editar e `PLAY` de novo.";

        /// <summary>Dispara na 1ª bubble visível de cada bucket do TF (filtro da tela no PLAY; ritmo no runner).</summary>
        public override bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx) =>
            TriggerChecks.IsVisibleBubble(ev, ctx.Filter);

        /// <summary>Config 100% no código (nada no tab).</summary>
        private const int LookbackCandles = 60;
        private const int LookbackBubbles = 30;

        public override async Task<StrategyDecision?> EvaluateAsync(
            StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default)
        {
            // TF da sessão (não do evento): bubble não carrega TF.
            var tf = ctx.TimeFrame;

            using var doc = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(ctx.ScreenSpecJson) ? "{}" : ctx.ScreenSpecJson);
            var snap = await _builder.BuildAsync(new ScreenStateRequest
            {
                Symbol = ctx.Symbol,
                TimeFrame = tf,
                DisplayDate = DateTime.Today,
                VisibleTimeFrames = new() { tf },
                Spec = doc.RootElement.Clone(),
                // JEV: escopo resolvido no backend + diário sempre.
                IncludeDaily = true,
                ResolveScope = true,
                LookbackCandles = LookbackCandles,
                LookbackBubbles = LookbackBubbles,
            }, ct);
            ctx.Snapshot = snap;

            var state = JevPrompt.BuildStateJson(ctx.Symbol, tf, snap);
            var stateJson = JsonSerializer.Serialize(state);
            var questions = JevPrompt.BuildQuestions();
            var weights = await JevWeights.LoadAsync(_keeper);
            var resp = await _jev.AskAsync(state, questions, ct);
            var res = JevPrompt.ParseDecision(ctx.Symbol, resp.Model, resp.Body,
                resp.InputTokens, resp.OutputTokens, stateJson, weights);
            // Auditoria best-effort: 1 arquivo por chamada em Data/JEV.
            // Nunca derruba a decisão (falha vira "" + warning no store).
            string auditFile = "";
            try
            {
                var header = snap.Get<HeaderView>(SnapshotLayers.Header);
                var record = JevPrompt.BuildAudit(
                    B3WM.Shared.Models.AI.JevAuditFiles.NewId(),
                    DateTime.Now, ev.At, ev.GetType().Name,
                    ctx.Symbol, tf,
                    header?.DisplayDate ?? DateTime.Today,
                    "strategy", resp.Model, ctx.SnapshotHash ?? "",
                    LookbackCandles, LookbackBubbles,
                    snap, stateJson,
                    JsonSerializer.Serialize(questions),
                    resp.HttpStatus, resp.LatencyMs, resp.Attempts,
                    resp.Body, res);
                auditFile = await _audit.TryWriteAsync(record, ct);
                res.AuditFile = auditFile;
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "[jev-audit] falha ao montar auditoria {Symbol}", ctx.Symbol);
            }
            return new StrategyDecision
            {
                Side = res.Direcao,
                Confidence = res.DirecaoConfidence,
                Criteria = res.Criteria,
                Reason = JevPrompt.FormatCriteria(res),
                StateChars = res.StatePreview.Length,
                AuditFile = auditFile,
            };
        }
    }
}
