using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Core;
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
    /// Avalia o gatilho em cada candle fechado / bubble e invoca a strategy.
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

        public StrategyRunner(
            IServiceScopeFactory scopes,
            IEnumerable<CandleService> candles,
            IEnumerable<BubbleService> bubbles,
            ILogger<StrategyRunner> logger)
        {
            _scopes = scopes;
            _logger = logger;
            foreach (var c in candles)
                c.OnUpdate += bar => OnCandleAsync(bar);
            foreach (var b in bubbles)
                b.OnUpdate += bubble => OnBubbleAsync(bubble);
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
            var mine = SessionsOf(bar.Symbol);
            if (mine.Count == 0) return;
            foreach (var s in mine)
            {
                if (!CheckTrigger(s, new CandleClosed { Symbol = bar.Symbol, At = bar.Date, Bar = bar }))
                    continue;
                await FireAsync(s, new CandleClosed { Symbol = bar.Symbol, At = bar.Date, Bar = bar }, "candle_close");
            }
            // Execuções pendentes no open do candle novo + zeragem na virada do dia.
            foreach (var s in SessionsOf(bar.Symbol))
                ExecutePending(s, bar.Open, bar.Date, "open seguinte");
            foreach (var s in SessionsOf(bar.Symbol))
                if (s.Position != null && bar.Date.Date > s.Position.EntryTime.Date)
                    ClosePaper(s, bar.Open, bar.Date, "virada do dia");
            foreach (var s in SessionsOf(bar.Symbol))
                if (s.Decisions.Count % 10 == 0 && s.Decisions.Count > s.PersistedDecisions)
                {
                    s.PersistedDecisions = s.Decisions.Count;
                    await PersistLogAsync(ToLogDay(s));
                }
        }

        private async Task OnBubbleAsync(BubbleStorageItem bubble)
        {
            var mine = SessionsOf(bubble.Symbol);
            if (mine.Count == 0) return;
            var ev = new BubbleAppeared { Symbol = bubble.Symbol, At = bubble.Date, Item = bubble };
            foreach (var s in mine)
            {
                if (!CheckTrigger(s, ev))
                    continue;
                await FireAsync(s, ev, "bubble");
            }
        }

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
            var item = new StrategyDecisionLog
            {
                Kind = "avaliacao",
                Time = ev.At.ToString("HH:mm"),
                Event = why,
                Side = decision.Side,
                Confidence = decision.Confidence,
                Encerrar = decision.Encerrar,
                ShouldTrade = decision.ShouldTrade,
                Reason = decision.Reason,
                StateChars = decision.StateChars,
                SnapshotHash = s.SnapshotHash,
                PositionBefore = posBefore,
                Action = action,
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
            if (s.Decisions.Count % 10 == 0)
            {
                s.PersistedDecisions = s.Decisions.Count;
                await PersistLogAsync(ToLogDay(s));
            }
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
                EntryTime = s.Position.EntryTime.ToString("HH:mm"),
                Entry = s.Position.Entry,
                ExitTime = at.ToString("HH:mm"),
                Exit = price,
                Pts = pts,
                ExitReason = reason,
                EntryConf = s.Position.EntryConf,
            });
            NoteLocked(s, new StrategyDecisionLog
            {
                Kind = "execucao",
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
            try
            {
                using var scope = _scopes.CreateScope();
                var keeper = scope.ServiceProvider.GetRequiredService<DataKeeperBase>();
                await keeper.WriteDataAsync(
                    $"{log.Symbol}_Strategy_{log.Strategy}_{log.Date:yyyy-MM-dd}.json", log);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[strategy-runner] falha ao persistir log");
            }
        }

        public static string SnapshotHashOf(string rawJson)
        {
            var bytes = Encoding.UTF8.GetBytes(rawJson);
            return Convert.ToHexString(SHA256.HashData(bytes))[..12];
        }
    }
}
