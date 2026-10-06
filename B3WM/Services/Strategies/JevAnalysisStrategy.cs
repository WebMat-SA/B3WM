using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Screen;

namespace B3WM.Services.Strategies
{
    /// <summary>
    /// Análise da IA como strategy: injeta JevService, monta o snapshot
    /// WYSIWYG a partir do spec cru da sessão e converte a resposta em
    /// StrategyDecision. Suporta gatilho de fechamento de candle (qualquer
    /// TF) e de bubble.
    /// </summary>
    public sealed class JevAnalysisStrategy : StrategyBase
    {
        private readonly JevService _jev;
        private readonly ScreenStateBuilder _builder;

        public JevAnalysisStrategy(
            ILogger<JevAnalysisStrategy> logger,
            JevService jev,
            ScreenStateBuilder builder) : base(logger)
        {
            _jev = jev;
            _builder = builder;
        }

        public override string Name => "JevAnalysis";
        public override string Description => "Análise Jev/TypeSafe do contexto (bolhas, volume, candles, topos/vales, pivots)";

        /// <summary>Dispara no fechamento do candle do TF da tela (foto do PLAY).</summary>
        public override bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx) =>
            TriggerChecks.IsCandleClose(ev, ctx.TimeFrame);

        /// <summary>Config 100% no código (nada no tab).</summary>
        private const double Threshold = 0.7;
        private const int LookbackCandles = 60;
        private const int LookbackBubbles = 30;

        public override async Task<StrategyDecision?> EvaluateAsync(
            StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default)
        {
            var tf = ev is CandleClosed cc ? cc.Bar.TimeFrame : 2;

            using var doc = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(ctx.ScreenSpecJson) ? "{}" : ctx.ScreenSpecJson);
            var snap = await _builder.BuildAsync(new ScreenStateRequest
            {
                Symbol = ctx.Symbol,
                TimeFrame = tf,
                DisplayDate = DateTime.Today,
                VisibleTimeFrames = new() { tf },
                Spec = doc.RootElement.Clone(),
                IncludeDaily = ScreenSpec.Bool(ScreenSpec.Daily(doc.RootElement), "panelVisible", false),
                LookbackCandles = LookbackCandles,
                LookbackBubbles = LookbackBubbles,
            }, ct);
            ApplyPosition(snap, ctx.Position);
            ctx.Snapshot = snap;

            var state = JevPrompt.BuildState(ctx.Symbol, tf, snap);
            var resp = await _jev.AskAsync(state, JevPrompt.BuildQuestions(), ct);
            var res = JevPrompt.ParseDecision(ctx.Symbol, resp.Model, resp.Body,
                resp.InputTokens, resp.OutputTokens, Threshold, state);
            return new StrategyDecision
            {
                Side = res.Direcao,
                Confidence = res.DirecaoConfidence,
                Encerrar = res.EncerrarPosicao,
                ShouldTrade = res.ShouldTrade,
                Reason = $"jev {res.Model} in={res.InputTokens} out={res.OutputTokens}",
                StateChars = res.StatePreview.Length,
            };
        }
    }
}
