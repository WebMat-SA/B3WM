using B3WM.Services.Screen;

namespace B3WM.Services.Strategies
{
    /// <summary>
    /// Contrato das estratégias que rodam no backend.
    /// `ShouldTrigger` (síncrono, barato, sem I/O) decide SE avalia, lendo só
    /// `ev` + `ctx` — as condições vivem aqui, no código da strategy.
    /// `EvaluateAsync` decide O QUÊ fazer (pode ser caro: Jev, arquivos).
    /// </summary>
    public interface IStrategy
    {
        string Name { get; }
        string Description { get; }
        IReadOnlyList<StrategyParam> DeclaredParams { get; }
        bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx);
        Task<StrategyDecision?> EvaluateAsync(StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default);
        void Reset();
    }

    /// <summary>
    /// Base com logger obrigatório: tudo que roda por trás passa por aqui
    /// (trigger avaliado, evaluate chamada, decisão, erro).
    /// </summary>
    public abstract class StrategyBase : IStrategy
    {
        protected readonly ILogger Log;

        protected StrategyBase(ILogger logger) => Log = logger;

        public abstract string Name { get; }
        public virtual string Description => Name;
        public virtual IReadOnlyList<StrategyParam> DeclaredParams => Array.Empty<StrategyParam>();
        public abstract bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx);
        public abstract Task<StrategyDecision?> EvaluateAsync(StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default);
        public virtual void Reset() { }

        protected static double ParamDouble(IReadOnlyDictionary<string, double> p, string key, double fallback) =>
            p != null && p.TryGetValue(key, out var v) ? v : fallback;

        protected void LogTrigger(string session, StrategyEvent ev, bool fired, string reason) =>
            Log.LogInformation("[strategy:{Name}:{Session}] trigger {Kind} {Verdict} ({Reason}) ev={EvAt}",
                Name, session, ev.GetType().Name, fired ? "FIRED" : "skipped", reason, ev.At);

        protected void LogDecision(string session, StrategyDecision? d) =>
            Log.LogInformation("[strategy:{Name}:{Session}] decision side={Side} conf={Conf:F2} encerrar={Enc:F2} shouldTrade={Trade} reason={Reason}",
                Name, session, d?.Side, d?.Confidence ?? 0, d?.Encerrar ?? 0, d?.ShouldTrade ?? false, d?.Reason);

        protected void LogError(string session, Exception ex, string what) =>
            Log.LogWarning(ex, "[strategy:{Name}:{Session}] error em {What}", Name, session, what);

        /// <summary>
        /// Injeta a posição paper no snapshot (bloco Posição). Sem posição =
        /// flat. Calcula parcial e candles segurados a partir das candles.
        /// </summary>
        protected static void ApplyPosition(
            MarketSnapshot snap, PaperPosition? pos)
        {
            if (pos == null) return;
            var candles = snap.Get<List<Shared.Models.BarStorageItem>>(
                SnapshotLayers.Candles) ?? new();
            var last = candles.Count > 0 ? candles[^1].Close : (double?)null;
            snap.Layers[SnapshotLayers.Position] = new PositionView
            {
                Side = pos.Side,
                Entry = pos.Entry,
                EntryTime = pos.EntryTime,
                UnrealizedPts = last == null ? 0 :
                    (last.Value - pos.Entry) * (pos.Side == "comprar" ? 1 : -1),
                Candles = candles.Count(b => b.Date > pos.EntryTime),
            };
        }
    }

    /// <summary>Registro dinâmico: todas as IStrategy no DI (uma linha por strategy nova).</summary>
    public sealed class StrategyRegistry
    {
        private readonly IEnumerable<IStrategy> _strategies;
        public StrategyRegistry(IEnumerable<IStrategy> strategies) => _strategies = strategies;

        public IReadOnlyList<IStrategy> All() => _strategies.ToList();

        public IStrategy Get(string name) =>
            _strategies.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Strategy desconhecida: {name}");
    }
}
