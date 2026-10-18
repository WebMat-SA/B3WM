using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;
using B3WM.Shared.Models.Strategies;
namespace B3WM.Services.Strategies
{
    /// <summary>Sessão armada: spec cru da tela + posição paper + relatório.</summary>
    public sealed class StrategySession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Strategy { get; set; } = "";
        public string Symbol { get; set; } = "";
        /// <summary>TF da tela no PLAY (filtros travados, não muda na sessão).</summary>
        public int TriggerTimeFrame { get; set; } = 2;
        /// <summary>
        /// Ritmo bubble: bucket do TF já avaliado (hora da bubble via
        /// GetCandleStart). Só a 1ª bubble visível de cada bucket dispara;
        /// filtro reprovado não consome o bucket. Runtime, sem persistência.
        /// </summary>
        public DateTime? LastBubbleBucket { get; set; }
        public string ScreenSpecJson { get; set; } = "{}";
        public BubbleFilter Filter { get; set; } = new();
        public string SnapshotHash { get; set; } = "";
        public bool Paused { get; set; }
        public PaperPosition? Position { get; set; }
        public string? PendingEntry { get; set; }
        public double PendingEntryConf { get; set; }
        public bool PendingExit { get; set; }
        public double PendingExitScore { get; set; }
        public List<StrategyDecisionLog> Decisions { get; set; } = new();
        public int PersistedDecisions { get; set; }
        public List<PaperTrade> PaperTrades { get; set; } = new();
        public DateTime StartedAt { get; set; } = DateTime.Now;
        public string? LastDecision { get; set; }
    }

    /// <summary>
    /// Executa as strategies armadas no backend (funciona com o app fechado).
    /// Fonte dos fatos: OrchestratorService por símbolo (candle enriquecido +
    /// bubble repassada); o runner consome e cada strategy decide no próprio
    /// `ShouldTrigger` (ex.: JevAnalysis só bubble, outra pode ser só
    /// candle_close). Runner genérico multi-trigger: despacha todo evento para
    /// todas as sessões do símbolo. Ritmo bubble: 1ª bubble visível
    /// por bucket do TF da sessão (pela hora da bubble). Candle serve também p/
    /// execução (open seguinte), zeragem na virada do dia e persistência.
    /// Log-only: o envio real está implementado mas COMENTADO (conta real: só
    /// descomentar sob flag explícita). Relatório paper em StrategyLogDay.
    /// </summary>
    public sealed class StrategyRunner : BackgroundService
    {
        /// <summary> 넓 Master switch do envio real. false = só loga WOULD-SEND.</summary>
        public const bool LiveTradingEnabled = false;

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<StrategyRunner> _logger;
        private readonly Dictionary<string, StrategySession> _sessions = new();
        private readonly object _lock = new();
        /// <summary>Serializa persistências do mesmo arquivo do dia (evita lost-update entre sessões).</summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _persistLocks = new();

        public StrategyRunner(
            IServiceScopeFactory scopes,
            IEnumerable<OrchestratorService> orchestrators,
            ILogger<StrategyRunner> logger)
        {
            _scopes = scopes;
            _logger = logger;
            // Padrão do projeto: o Core (OrchestratorService, por símbolo)
            // captura/publica os fatos (candle enriquecido, bubble); o runner
            // consome e cada strategy decide no ShouldTrigger.
            foreach (var o in orchestrators)
            {
                o.OnCandleClosed += (bar, _) => OnCandleAsync(bar);
                o.OnBubble += bubble => OnBubbleAsync(bubble);
            }
        }

        public IReadOnlyList<StrategySessionState> States()
        {
            lock (_lock)
                return _sessions.Values.Select(s => new StrategySessionState
                {
                    SessionId = s.Id,
                    Strategy = s.Strategy,
                    Symbol = s.Symbol,
                    Paused = s.Paused,
                    SnapshotHash = s.SnapshotHash,
                    Position = s.Position == null ? "flat"
                        : $"{s.Position.Side} @{s.Position.Entry:F0}",
                    Decisions = s.Decisions.Count,
                    RealizedPts = s.PaperTrades.Sum(t => t.Pts),
                    StartedAt = s.StartedAt,
                    LastDecision = s.LastDecision,
                    // Todos do dia, mais recentes primeiro (a aba mostra de cima p/ baixo).
                    ReportItems = s.Decisions.AsEnumerable().Reverse().ToList(),
                }).ToList();
        }

        public string Arm(string strategy, string symbol, int triggerTimeFrame,
            string screenSpecJson, BubbleFilter filter, string snapshotHash)
        {
            var session = new StrategySession
            {
                Strategy = strategy,
                Symbol = symbol,
                TriggerTimeFrame = triggerTimeFrame,
                ScreenSpecJson = screenSpecJson,
                Filter = filter,
                SnapshotHash = snapshotHash,
            };
            lock (_lock) _sessions[session.Id] = session;
            _logger.LogInformation("[strategy-runner] ARM {Strategy} {Symbol} tf={Tf} snap={Hash} sessão={Id}",
                strategy, symbol, triggerTimeFrame, snapshotHash, session.Id);
            return session.Id;
        }

        public bool Pause(string id, bool paused)
        {
            lock (_lock)
            {
                if (!_sessions.TryGetValue(id, out var s)) return false;
                s.Paused = paused;
            }
            _logger.LogInformation("[strategy-runner] sessão {Id} paused={Paused}", id, paused);
            return true;
        }

        public StrategyLogDay? Stop(string id)
        {
            StrategySession? s;
            lock (_lock)
            {
                if (!_sessions.TryGetValue(id, out s)) return null;
                _sessions.Remove(id);
            }
            var log = ToLogDay(s);
            _ = PersistLogAsync(log);
            _logger.LogInformation("[strategy-runner] STOP {Strategy} {Symbol} sessão={Id} trades={N} pts={Pts:+0;-0}",
                s.Strategy, s.Symbol, id, log.PaperTrades.Count, log.RealizedPts);
            return log;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            Task.CompletedTask; // dirigido por eventos; sem loop

        private async Task OnCandleAsync(BarStorageItem bar)
        {
            if (SessionsOf(bar.Symbol).Count == 0) return;
            // Despacha o evento para todas as sessões do símbolo; cada
            // strategy filtra no ShouldTrigger (candle_close, bubble, etc.).
            await PublishAsync(
                new CandleClosed { Symbol = bar.Symbol, At = bar.Date, Bar = bar },
                "candle_close");
            // Execução amarrada ao TF da sessão: pendências e zeragem diária
            // só no open do candle do próprio TF (não no de qualquer TF).
            foreach (var s in SessionsOf(bar.Symbol).Where(s => ShouldExecuteOnBar(s, bar)))
                ExecutePending(s, bar.Open, bar.Date, $"open seguinte TF{bar.TimeFrame}");
            foreach (var s in SessionsOf(bar.Symbol).Where(s => ShouldExecuteOnBar(s, bar)))
                if (s.Position != null && bar.Date.Date > s.Position.EntryTime.Date)
                    ClosePaper(s, bar.Open, bar.Date, "virada do dia");
            foreach (var s in SessionsOf(bar.Symbol))
                // Flush de safety-net: cobre execuções (exec abre/fecha) e virada
                // do dia que adicionam Decisions fora do FireAsync. Com flush a
                // cada decisão, aqui só persiste se houver algo novo.
                if (s.Decisions.Count > s.PersistedDecisions)
                {
                    s.PersistedDecisions = s.Decisions.Count;
                    await PersistLogAsync(ToLogDay(s));
                }
        }

        private async Task OnBubbleAsync(BubbleStorageItem bubble)
        {
            if (SessionsOf(bubble.Symbol).Count == 0) return;
            await PublishAsync(
                new BubbleAppeared { Symbol = bubble.Symbol, At = bubble.Date, Item = bubble },
                "bubble");
        }

        /// <summary>
        /// Ponto de extensão para qualquer trigger atual ou futuro
        /// (candle_close, bubble, toque em pivot, etc.): despacha `ev` para
        /// todas as sessões não-pausadas do símbolo; cada strategy decide no
        /// `ShouldTrigger`. Futuro: basta criar o `StrategyEvent` novo e chamar aqui.
        /// Ritmo bubble: só a 1ª bubble visível de cada bucket do TF da sessão
        /// dispara (bucket pela hora da bubble, `ev.At`); filtro reprovado não
        /// consome o bucket; bucket sem bubble = silêncio (sem fallback).
        /// </summary>
        public async Task PublishAsync(StrategyEvent ev, string why)
        {
            var mine = SessionsOf(ev.Symbol);
            if (mine.Count == 0) return;
            foreach (var s in mine)
            {
                if (ev is BubbleAppeared)
                {
                    var bucket = BubbleBucketFor(ev.At, s.TriggerTimeFrame);
                    bool alreadyFired;
                    lock (_lock)
                        alreadyFired = !IsNewBubbleBucket(s.LastBubbleBucket, bucket);
                    if (alreadyFired)
                    {
                        _logger.LogDebug("[strategy:{Name}:{Id}] bubble skip bucket {Bucket:HH:mm} já avaliado",
                            s.Strategy, s.Id, bucket);
                        continue;
                    }
                    if (!CheckTrigger(s, ev))
                        continue; // filtro reprovou: bucket segue livre
                    lock (_lock) { s.LastBubbleBucket = bucket; }
                    await FireAsync(s, ev, why);
                    continue;
                }
                if (!CheckTrigger(s, ev))
                    continue;
                await FireAsync(s, ev, why);
            }
        }

        /// <summary>
        /// Bucket do TF pela hora da bubble (mesmo alinhamento do CandleService).
        /// Testável sem runner (puro).
        /// </summary>
        public static DateTime BubbleBucketFor(DateTime bubbleTime, int triggerTimeFrame) =>
            bubbleTime.GetCandleStart(triggerTimeFrame <= 0 ? 2 : triggerTimeFrame);

        /// <summary>
        /// Gate puro: true se o bucket ainda não foi avaliado pela sessão.
        /// Testável sem runner.
        /// </summary>
        public static bool IsNewBubbleBucket(DateTime? lastBucket, DateTime bucket) =>
            !lastBucket.HasValue || lastBucket.Value != bucket;

        /// <summary>
        /// Execução amarrada ao TF: a sessão só executa pendências/virada no
        /// open do candle do próprio TF da foto do PLAY. Testável sem runner.
        /// </summary>
        public static bool ShouldExecuteOnBar(StrategySession s, BarStorageItem bar) =>
            s.TriggerTimeFrame == bar.TimeFrame;

        /// <summary>
        /// O contrato: cada strategy decide no próprio ShouldTrigger.
        /// Requer a instância (resolve no escopo); erro = skip + log.
        /// </summary>
        private bool CheckTrigger(StrategySession s, StrategyEvent ev)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var strategy = scope.ServiceProvider.GetServices<IStrategy>()
                    .FirstOrDefault(x => string.Equals(x.Name, s.Strategy, StringComparison.OrdinalIgnoreCase));
                if (strategy == null) return false;
                var tctx = new StrategyTriggerContext
                {
                    Symbol = s.Symbol,
                    TimeFrame = s.TriggerTimeFrame,
                    Filter = s.Filter,
                };
                var fired = strategy.ShouldTrigger(ev, tctx);
                if (!fired)
                    _logger.LogDebug("[strategy:{Name}:{Id}] trigger skipped {Ev}",
                        s.Strategy, s.Id, ev.GetType().Name);
                return fired;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[strategy-runner] erro no trigger {Id}", s.Id);
                return false;
            }
        }

        private List<StrategySession> SessionsOf(string symbol)
        {
            lock (_lock)
                return _sessions.Values.Where(s => !s.Paused && s.Symbol == symbol).ToList();
        }

        private async Task FireAsync(StrategySession s, StrategyEvent ev, string why)
        {
            using var scope = _scopes.CreateScope();
            var sp = scope.ServiceProvider;
            IStrategy strategy;
            try
            {
                strategy = sp.GetServices<IStrategy>()
                    .FirstOrDefault(x => string.Equals(x.Name, s.Strategy, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"Strategy desconhecida: {s.Strategy}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[strategy-runner] sessão {Id} sem strategy", s.Id);
                return;
            }

            var ctx = new StrategyContext
            {
                Symbol = s.Symbol,
                TimeFrame = s.TriggerTimeFrame,
                ScreenSpecJson = s.ScreenSpecJson,
                SnapshotHash = s.SnapshotHash,
                Position = s.Position == null ? null : new PaperPosition
                {
                    Side = s.Position.Side, Entry = s.Position.Entry,
                    EntryTime = s.Position.EntryTime, EntryConf = s.Position.EntryConf,
                },
            };
            StrategyDecision? decision = null;
            try
            {
                decision = await strategy.EvaluateAsync(ev, ctx);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[strategy:{Name}:{Id}] erro no Evaluate", s.Strategy, s.Id);
                return;
            }
            if (decision == null) return;

            // Threshold de saída do paper engine (política do runner; a
            // strategy só informa Encerrar 0..1).
            const double exitThr = 0.5;
            var posBefore = s.Position == null ? "flat"
                : $"{(s.Position.Side == "comprar" ? "comprado" : "vendido")} @{s.Position.Entry:F0}";
            var (action, wantEntry, wantExit) = DescribeEvaluation(decision, s.Position != null, exitThr);
            var evalAt = EvaluationTime(ev);
            var item = new StrategyDecisionLog
            {
                Kind = "avaliacao",
                // Data/hora completa da AVALIAÇÃO (quando a strategy foi chamada), não da abertura:
                // candle -> fechamento (open + timeframe); bubble/futuros -> hora do evento.
                At = evalAt,
                Time = evalAt.ToString("HH:mm"),
                Event = why,
                Side = decision.Side,
                Confidence = decision.Confidence,
                Encerrar = decision.Encerrar,
                ShouldTrade = decision.ShouldTrade,
                Criteria = decision.Criteria,
                Reason = decision.Reason,
                StateChars = decision.StateChars,
                SnapshotHash = s.SnapshotHash,
                PositionBefore = posBefore,
                Action = action,
                AuditFile = decision.AuditFile ?? "",
            };
            lock (_lock)
            {
                NoteLocked(s, item);
                if (wantEntry != null)
                {
                    s.PendingEntry = wantEntry;
                    s.PendingEntryConf = decision.Confidence;
                }
                else if (wantExit)
                {
                    s.PendingExit = true;
                    s.PendingExitScore = decision.Encerrar;
                }
            }
            _logger.LogInformation("[strategy:{Name}:{Id}] {Summary} ({Why})",
                s.Strategy, s.Id, Summarize(item), why);
            // Flush a cada decisão: sobrevive a pico de energia / kill sem STOP.
            // Custo ~ms (JSON pequeno do dia) frente à latência do Jev.
            s.PersistedDecisions = s.Decisions.Count;
            await PersistLogAsync(ToLogDay(s));
        }

        /// <summary>
        /// Regra pura do paper engine na avaliação (testável sem runner):
        /// devolve (ação descrita, lado p/ entrada pendente?, saída pendente?).
        /// </summary>
        public static (string Action, string? WantEntry, bool WantExit) DescribeEvaluation(
            StrategyDecision decision, bool hasPosition, double exitThr)
        {
            if (!hasPosition && decision.ShouldTrade &&
                (decision.Side == "comprar" || decision.Side == "vender"))
                return ($"sinal abre {decision.Side} (conf {decision.Confidence:F2})", decision.Side, false);
            if (hasPosition && decision.Encerrar > exitThr)
                return ($"sinal fecha (encerrar {decision.Encerrar:F2})", null, true);
            return ("manter", null, false);
        }

        private static string Summarize(StrategyDecisionLog item) =>
            $"{item.Time} {item.Side} conf={item.Confidence:F2} enc={item.Encerrar:F2} snap={item.SnapshotHash}";

        /// <summary>
        /// Data/hora canônica do relatório = momento da avaliação: fechamento do
        /// candle (abertura + timeframe) ou hora do evento (bubble).
        /// </summary>
        public static DateTime EvaluationTime(StrategyEvent ev) =>
            ev is CandleClosed cc ? cc.Bar.Date.AddMinutes(cc.Bar.TimeFrame) : ev.At;

        /// <summary>Registra item + resumo (chamar com _lock).</summary>
        private void NoteLocked(StrategySession s, StrategyDecisionLog item)
        {
            s.Decisions.Add(item);
            s.LastDecision = Summarize(item);
        }

        private void ExecutePending(StrategySession s, double open, DateTime at, string execReason)
        {
            lock (_lock)
            {
                if (s.PendingEntry != null && s.Position == null)
                {
                    s.Position = new PaperPosition
                    {
                        Side = s.PendingEntry, Entry = open,
                        EntryTime = at, EntryConf = s.PendingEntryConf,
                    };
                    // ENVIO REAL (conta real futura): descomentar sob flag explícita.
                    // if (LiveTradingEnabled) { _ = SendLiveOrderAsync(s, s.Position); }
                    _logger.LogInformation(
                        "[strategy:{Name}:{Id}] WOULD-SEND {Side} 1 @{Price:F0} ({Reason}) snap={Hash} [paper]",
                        s.Strategy, s.Id, s.Position.Side, open, execReason, s.SnapshotHash);
                    NoteLocked(s, new StrategyDecisionLog
                    {
                        Kind = "execucao",
                        At = at,
                        Time = at.ToString("HH:mm"),
                        Event = "exec",
                        Side = s.Position.Side,
                        Confidence = s.Position.EntryConf,
                        SnapshotHash = s.SnapshotHash,
                        PositionBefore = "flat",
                        Action = $"exec abre {s.Position.Side} @{open:F0}",
                    });
                    s.PendingEntry = null;
                }
                if (s.PendingExit && s.Position != null)
                {
                    ClosePaperLocked(s, open, at, "sinal encerrar");
                    s.PendingExit = false;
                }
            }
        }

        private void ClosePaper(StrategySession s, double price, DateTime at, string reason)
        {
            lock (_lock) ClosePaperLocked(s, price, at, reason);
        }

        private void ClosePaperLocked(StrategySession s, double price, DateTime at, string reason)
        {
            if (s.Position == null) return;
            var pts = (price - s.Position.Entry) * (s.Position.Side == "comprar" ? 1 : -1);
            // ENVIO REAL (conta real futura): descomentar sob flag explícita.
            // if (LiveTradingEnabled) { _ = SendLiveOrderAsync(s, null); }
            _logger.LogInformation(
                "[strategy:{Name}:{Id}] WOULD-SEND fecha {Side} @{Price:F0} ({Pts:+0;-0} pts, {Reason}) snap={Hash} [paper]",
                s.Strategy, s.Id, s.Position.Side, price, pts, reason, s.SnapshotHash);
            s.PaperTrades.Add(new PaperTrade
            {
                Side = s.Position.Side,
                EntryAt = s.Position.EntryTime,
                EntryTime = s.Position.EntryTime.ToString("HH:mm"),
                Entry = s.Position.Entry,
                ExitAt = at,
                ExitTime = at.ToString("HH:mm"),
                Exit = price,
                Pts = pts,
                ExitReason = reason,
                EntryConf = s.Position.EntryConf,
            });
            NoteLocked(s, new StrategyDecisionLog
            {
                Kind = "execucao",
                At = at,
                Time = at.ToString("HH:mm"),
                Event = reason,
                Side = s.Position.Side,
                SnapshotHash = s.SnapshotHash,
                PositionBefore = $"{(s.Position.Side == "comprar" ? "comprado" : "vendido")} @{s.Position.Entry:F0}",
                Action = $"fecha @{price:F0} ({pts:+0;-0} pts)",
            });
            s.Position = null;
            s.PendingExit = false;
        }

        // ENVIO REAL (conta real futura): implementar via forward ao Python/MT5
        // (mesmo padrão do TradeController: IHttpClientFactory "PythonService"
        // POST /api/order/market). Manter COMENTADO até decisão explícita.
        // private async Task SendLiveOrderAsync(StrategySession s, PaperPosition? position)
        // {
        //     var http = _scopes.CreateScope().ServiceProvider
        //         .GetRequiredService<IHttpClientFactory>().CreateClient("PythonService");
        //     var payload = position == null
        //         ? new { action = "close", symbol = s.Symbol }
        //         : new { action = "market", symbol = s.Symbol, side = position.Side, quantity = 1 };
        //     var content = new StringContent(
        //         JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        //     var resp = await http.PostAsync("/api/order/market", content);
        //     _logger.LogWarning("[strategy:{Name}:{Id}] LIVE order HTTP {Status}", s.Strategy, s.Id, (int)resp.StatusCode);
        // }

        private StrategyLogDay ToLogDay(StrategySession s)
        {
            lock (_lock)
            {
                return new StrategyLogDay
                {
                    Date = DateTime.Today,
                    Symbol = s.Symbol,
                    Strategy = s.Strategy,
                    SnapshotHash = s.SnapshotHash,
                    Decisions = s.Decisions.ToList(),
                    PaperTrades = s.PaperTrades.ToList(),
                    RealizedPts = s.PaperTrades.Sum(t => t.Pts),
                };
            }
        }

        private async Task PersistLogAsync(StrategyLogDay log)
        {
            var path = $"{log.Symbol}_Strategy_{log.Strategy}_{log.Date:yyyy-MM-dd}.json";
            var gate = _persistLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                using var scope = _scopes.CreateScope();
                var keeper = scope.ServiceProvider.GetRequiredService<DataKeeperBase>();
                // O arquivo do dia ACUMULA todas as sessões: um novo PLAY/STOP
                // faz merge com o que já está em disco em vez de sobrescrever
                // (antes, a sessão anterior do mesmo dia era perdida).
                // Flush a cada decisão: Read+Merge+Write por chamada, atômico
                // via .tmp+Move no DataKeeperBase; gate por arquivo evita
                // lost-update entre sessões concorrentes do mesmo dia.
                var existing = await keeper.ReadDataAsync<StrategyLogDay>(path);
                if (existing.Decisions.Count > 0 || existing.PaperTrades.Count > 0)
                    log = MergeLogDay(existing, log);
                await keeper.WriteDataAsync(path, log);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[strategy-runner] falha ao persistir log");
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Merge puro do log do dia (testável sem runner): une decisões e
        /// trades sem duplicar, ordena por At e recalcula o realizado.
        /// SnapshotHash do dia mantém o da primeira sessão (cada item carrega
        /// o próprio snapshot).
        /// </summary>
        public static StrategyLogDay MergeLogDay(StrategyLogDay existing, StrategyLogDay incoming)
        {
            var seen = new HashSet<string>(existing.Decisions.Select(DecisionKey));
            foreach (var d in incoming.Decisions)
                if (seen.Add(DecisionKey(d)))
                    existing.Decisions.Add(d);
            var seenTrades = new HashSet<string>(existing.PaperTrades.Select(TradeKey));
            foreach (var t in incoming.PaperTrades)
                if (seenTrades.Add(TradeKey(t)))
                    existing.PaperTrades.Add(t);
            existing.Decisions.Sort((a, b) =>
            {
                var c = a.At.CompareTo(b.At);
                return c != 0 ? c : string.Compare(a.Time, b.Time, StringComparison.Ordinal);
            });
            existing.RealizedPts = existing.PaperTrades.Sum(t => t.Pts);
            return existing;
        }

        private static string DecisionKey(StrategyDecisionLog d) =>
            $"{(d.At == default ? d.Time : d.At.ToString("o"))}|{d.Kind}|{d.Side}|{d.Action}|{d.SnapshotHash}";

        private static string TradeKey(PaperTrade t) =>
            $"{(t.EntryAt == default ? t.EntryTime : t.EntryAt.ToString("o"))}|{t.Side}|{t.Entry}|{(t.ExitAt == default ? t.ExitTime : t.ExitAt.ToString("o"))}|{t.Exit}";

        public static string SnapshotHashOf(string rawJson)
        {
            var bytes = Encoding.UTF8.GetBytes(rawJson);
            return Convert.ToHexString(SHA256.HashData(bytes))[..12];
        }
    }
}
