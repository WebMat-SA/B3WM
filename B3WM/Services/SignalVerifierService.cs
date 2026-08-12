using B3WM.Services.Backtest;
using B3WM.Services.Core;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services
{
    /// <summary>
    /// Verificador de lógica ao vivo (paper trading) por símbolo+timeframe.
    /// Simulação 100% isolada da conta real; não envia ordens. Estado em memória,
    /// com log diário (JSON) dos sinais/trades para persistência e export.
    /// </summary>
    public class SignalVerifierService : IDisposable
    {
        private readonly string _symbol;
        private readonly int _timeFrame;
        private readonly IHubContext<DataHub, IDataHubClient> _hub;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SignalVerifierService> _logger;

        private readonly object _stateLock = new();
        private VerifierConfig _config;
        private BacktestSimulator? _simulator;
        private IStrategy? _strategy;
        private StructureService? _structureService;

        private readonly List<SignalEvent> _signals = new();
        private readonly List<double> _equityCurve = new();
        private bool _running;
        private DateTime _logDay;

        public string Symbol => _symbol;
        public int TimeFrame => _timeFrame;

        public SignalVerifierService(
            string symbol,
            int timeFrame,
            VerifierConfig config,
            IHubContext<DataHub, IDataHubClient> hub,
            IServiceProvider serviceProvider,
            ILogger<SignalVerifierService> logger)
        {
            _symbol = symbol;
            _timeFrame = timeFrame;
            _config = config;
            _hub = hub;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Start()
        {
            lock (_stateLock)
            {
                if (_running) return;

                var backtestConfig = _config.ToBacktestConfig();
                var logger = _serviceProvider.GetRequiredService<ILogger<SmartBreakoutStrategy>>();

                _strategy = _config.StrategyName switch
                {
                    StrategyType.Breakout => new SimpleBreakoutStrategy(_config.LookbackPeriod),
                    StrategyType.SmartBreakout => CreateSmartBreakout(backtestConfig, logger),
                    _ => throw new ArgumentException($"Unknown strategy: {_config.StrategyName}")
                };

                _simulator = new BacktestSimulator(backtestConfig, _strategy);
                _signals.Clear();
                _equityCurve.Clear();
                _running = true;
                _logDay = DateTime.Today;

                var orchestrator = _serviceProvider.GetServices<OrchestratorService>().FirstOrDefault(o => o.Symbol == _symbol);
                var bubble = _serviceProvider.GetServices<BubbleService>().FirstOrDefault(b => b.Symbol == _symbol);

                if (orchestrator != null) orchestrator.OnCandleClosed += OnCandleClosed;
                if (bubble != null && _strategy is SmartBreakoutStrategy) bubble.OnUpdate += OnBubble;
            }
        }

        private SmartBreakoutStrategy CreateSmartBreakout(BacktestConfig config, ILogger<SmartBreakoutStrategy> logger)
        {
            _structureService = _serviceProvider.GetServices<StructureService>()
                .FirstOrDefault(s => s.Symbol == _symbol && s.TimeFrame == _timeFrame);

            return new SmartBreakoutStrategy(
                null,
                config,
                logger,
                _ => _structureService?.GetLastStructure());
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                if (!_running) return;
                _running = false;

                var orchestrator = _serviceProvider.GetServices<OrchestratorService>().FirstOrDefault(o => o.Symbol == _symbol);
                var bubble = _serviceProvider.GetServices<BubbleService>().FirstOrDefault(b => b.Symbol == _symbol);

                if (orchestrator != null) orchestrator.OnCandleClosed -= OnCandleClosed;
                if (bubble != null) bubble.OnUpdate -= OnBubble;

                _ = SaveLogAsync();
            }
        }

        public void Reset()
        {
            lock (_stateLock)
            {
                _signals.Clear();
                _equityCurve.Clear();
                _simulator?.Reset();
                _logDay = DateTime.Today;
            }
        }

        public VerifierState GetState()
        {
            lock (_stateLock)
            {
                var trades = _simulator?.Trades ?? new List<BacktestTrade>();
                var win = trades.Count(t => t.ProfitLoss > 0);

                return new VerifierState
                {
                    Symbol = _symbol,
                    TimeFrame = _timeFrame,
                    IsRunning = _running,
                    Config = _config,
                    OpenPosition = MapPosition(_simulator?.OpenPosition),
                    PendingSignal = MapPending(_simulator?.PendingEntry),
                    TotalTrades = trades.Count,
                    WinCount = win,
                    LossCount = trades.Count - win,
                    WinRate = trades.Count > 0 ? (double)win / trades.Count : 0,
                    NetProfit = _simulator?.NetProfit ?? 0,
                    GrossProfit = trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss),
                    GrossLoss = Math.Abs(trades.Where(t => t.ProfitLoss <= 0).Sum(t => t.ProfitLoss)),
                    MaxDrawdown = _simulator?.MaxDrawdown ?? 0,
                    Signals = _signals.TakeLast(200).Reverse().ToList(),
                    EquityCurve = new List<double>(_equityCurve)
                };
            }
        }

        private async Task OnCandleClosed(BarStorageItem bar, StructureStorageItem structure)
        {
            if (!_running || bar.TimeFrame != _timeFrame) return;

            List<SignalEvent> events;
            lock (_stateLock)
            {
                _logDay = bar.Date.Date;
                if (_simulator == null) return;
                events = _simulator.ProcessBar(bar);
                _signals.AddRange(events);
                _equityCurve.Clear();
                _equityCurve.AddRange(_simulator.EquityCurve);
            }

            foreach (var e in events)
            {
                if (_hub != null)
                    await _hub.Clients.Group(_symbol).ReceiveOnSignal(e);
            }

            if (events.Count > 0)
                await SaveLogAsync();
        }

        private async Task OnBubble(BubbleStorageItem bubble)
        {
            if (!_running || !PassesBubbleFilter(bubble)) return;

            IStrategy? s;
            lock (_stateLock) { s = _strategy; }
            if (s is SmartBreakoutStrategy smart)
                smart.AddBubble(bubble);
        }

        /// <summary>
        /// Mesmo filtro do gráfico: bubble visível quando
        /// Amount &gt;= threshold do agente (padrão do símbolo ou customizado) e
        /// agente dentro da seleção da aba Bubbles.
        /// </summary>
        private bool PassesBubbleFilter(BubbleStorageItem b)
        {
            var threshold = _config.AgentThresholds != null &&
                            _config.AgentThresholds.TryGetValue(b.Agent, out var custom) &&
                            custom > 0
                ? custom
                : (_config.BubbleThreshold > 0
                    ? _config.BubbleThreshold
                    : Defaults.GetThresholdBubble(_symbol));

            if ((double)b.Amount < threshold) return false;

            if (_config.SmartAgents is { Count: > 0 } && !_config.SmartAgents.Contains(b.Agent))
                return false;

            return true;
        }

        private async Task SaveLogAsync()
        {
            try
            {
                VerifierLogDay day;
                lock (_stateLock)
                {
                    var trades = _simulator?.Trades ?? new List<BacktestTrade>();
                    var win = trades.Count(t => t.ProfitLoss > 0);

                    day = new VerifierLogDay
                    {
                        Symbol = _symbol,
                        TimeFrame = _timeFrame,
                        Date = _logDay,
                        Config = _config,
                        Signals = new List<SignalEvent>(_signals),
                        TotalTrades = trades.Count,
                        WinCount = win,
                        LossCount = trades.Count - win,
                        WinRate = trades.Count > 0 ? (double)win / trades.Count : 0,
                        NetProfit = _simulator?.NetProfit ?? 0,
                        GrossProfit = trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss),
                        GrossLoss = Math.Abs(trades.Where(t => t.ProfitLoss <= 0).Sum(t => t.ProfitLoss)),
                        MaxDrawdown = _simulator?.MaxDrawdown ?? 0,
                        EquityCurve = new List<double>(_equityCurve)
                    };
                }

                using var scope = _serviceProvider.CreateScope();
                var dataKeeper = scope.ServiceProvider.GetRequiredService<DataKeeperBase>();
                var path = $"{_symbol}_Verifier_{_timeFrame}MIN_{_logDay:yyyy-MM-dd}.json";
                await dataKeeper.WriteDataAsync(path, day);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verifier log save failed for {Symbol}/{TF}", _symbol, _timeFrame);
            }
        }

        private static VerifierPosition? MapPosition(BacktestPosition? p)
        {
            if (p == null) return null;
            return new VerifierPosition
            {
                Side = p.Side,
                EntryPrice = p.EntryPrice,
                StopPrice = p.StopPrice,
                TargetPrice = p.TargetPrice,
                Quantity = p.Quantity,
                EntryDate = p.EntryDate,
                EntryReason = p.EntryReason
            };
        }

        private static VerifierPendingSignal? MapPending(Signal? s)
        {
            if (s == null) return null;
            return new VerifierPendingSignal
            {
                Side = s.Side,
                Reason = s.Reason,
                StopLossPrice = s.StopLossPrice,
                TakeProfitPrice = s.TakeProfitPrice
            };
        }

        public void Dispose() => Stop();
    }
}
