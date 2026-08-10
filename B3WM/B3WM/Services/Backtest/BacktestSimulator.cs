using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;

namespace B3WM.Services.Backtest
{
    internal class BacktestPosition
    {
        public OrderSide Side { get; set; }
        public double EntryPrice { get; set; }
        public double StopPrice { get; set; }
        public double TargetPrice { get; set; }
        public int Quantity { get; set; }
        public DateTime EntryDate { get; set; }
        public string? EntryReason { get; set; }
    }

    /// <summary>
    /// Simulador candle a candle, incremental. É usado tanto pelo backtest
    /// (loop sobre os candles salvos) quanto pelo verificador ao vivo
    /// (um candle fechado por vez), garantindo live == backtest.
    /// Sem slippage; com guarda de day trade (não abre após o close time).
    /// </summary>
    public class BacktestSimulator
    {
        private readonly BacktestConfig _config;
        private readonly IStrategy _strategy;
        private readonly double _pointValue;

        private BacktestPosition? _position;
        private Signal? _pendingEntry;

        private readonly List<BacktestTrade> _trades = new();
        private double _cumulativePL;
        private double _peak;
        private double _maxDd;
        private readonly List<double> _equityCurve = new() { 0 };

        public IReadOnlyList<BacktestTrade> Trades => _trades;
        public IReadOnlyList<double> EquityCurve => _equityCurve;
        public double NetProfit => _cumulativePL;
        public double MaxDrawdown => _maxDd;
        internal BacktestPosition? OpenPosition => _position;
        internal Signal? PendingEntry => _pendingEntry;
        public BacktestConfig Config => _config;

        public BacktestSimulator(BacktestConfig config, IStrategy strategy)
        {
            _config = config;
            _strategy = strategy;
            _pointValue = Defaults.GetPointValue(config.Symbol);
        }

        public void Reset()
        {
            _position = null;
            _pendingEntry = null;
            _trades.Clear();
            _cumulativePL = 0;
            _peak = 0;
            _maxDd = 0;
            _equityCurve.Clear();
            _equityCurve.Add(0);
            _strategy.Reset();
        }

        /// <summary>Processa um candle fechado e devolve os eventos (entrada/saída) gerados nele.</summary>
        public List<SignalEvent> ProcessBar(BarStorageItem bar)
        {
            var events = new List<SignalEvent>();

            // 1. Execução de entrada pendente (sinal da barra anterior)
            if (_pendingEntry != null && _position == null)
            {
                var entryPrice = bar.Open;
                var closeTime = ParseCloseTime();

                // guarda de day trade: não abrir posição com horário em/após o close time
                if (_config.IsDayTrade && closeTime.HasValue && bar.Date.TimeOfDay >= closeTime.Value)
                {
                    _pendingEntry = null;
                }
                else
                {
                    _position = new BacktestPosition
                    {
                        Side = _pendingEntry.Side,
                        EntryPrice = entryPrice,
                        StopPrice = _pendingEntry.StopLossPrice,
                        TargetPrice = _pendingEntry.TakeProfitPrice,
                        Quantity = _pendingEntry.Quantity > 0 ? _pendingEntry.Quantity : _config.Quantity,
                        EntryDate = bar.Date,
                        EntryReason = _pendingEntry.Reason
                    };

                    events.Add(new SignalEvent
                    {
                        Symbol = bar.Symbol,
                        TimeFrame = bar.TimeFrame,
                        Date = bar.Date,
                        Type = _pendingEntry.Side == OrderSide.Buy ? SignalType.EntryBuy : SignalType.EntrySell,
                        Side = _pendingEntry.Side,
                        EntryPrice = entryPrice,
                        StopPrice = _pendingEntry.StopLossPrice,
                        TargetPrice = _pendingEntry.TakeProfitPrice,
                        Quantity = _position.Quantity,
                        Reason = _pendingEntry.Reason,
                        PositionOpen = true
                    });

                    _pendingEntry = null;
                }
            }

            // 2. SL/TP intrabar (pessimista: stop antes de alvo)
            if (_position != null)
            {
                if (TryCloseByPrice(bar, _position, out var exitEvent))
                {
                    events.Add(exitEvent!);
                    _position = null;
                }
            }

            // 3. Reversão pela estratégia
            if (_position != null)
            {
                var signal = _strategy.Evaluate(bar, hasPosition: true);
                if (signal != null)
                {
                    events.Add(CloseTrade(_position, bar.Close, ExitReason.StrategySignal, bar.Date));
                    _position = null;
                }
            }

            // 4. Fechamento de day trade
            if (_position != null && _config.IsDayTrade)
            {
                var closeTime = ParseCloseTime();
                if (closeTime.HasValue && bar.Date.TimeOfDay >= closeTime.Value)
                {
                    events.Add(CloseTrade(_position, bar.Close, ExitReason.DayTradeClose, bar.Date));
                    _position = null;
                }
            }

            // 5. Novo sinal (sem posição e sem sinal pendente)
            if (_position == null && _pendingEntry == null)
            {
                var signal = _strategy.Evaluate(bar, hasPosition: false);
                if (signal != null && signal.IsValidEntry)
                {
                    _pendingEntry = signal;
                }
            }

            return events;
        }

        /// <summary>Fecha posição em aberto (ex.: fim dos dados no backtest).</summary>
        public List<SignalEvent> ForceClose(ExitReason reason, double exitPrice, DateTime exitDate)
        {
            if (_position == null) return new List<SignalEvent>();
            var ev = CloseTrade(_position, exitPrice, reason, exitDate);
            _position = null;
            return new List<SignalEvent> { ev };
        }

        private TimeSpan? ParseCloseTime()
        {
            return TimeSpan.TryParse(_config.DayTradeCloseTime, out var t) ? t : null;
        }

        private bool TryCloseByPrice(BarStorageItem bar, BacktestPosition position, out SignalEvent? ev)
        {
            ev = null;

            if (position.Side == OrderSide.Buy)
            {
                if (bar.Low <= position.StopPrice)
                {
                    ev = CloseTrade(position, position.StopPrice, ExitReason.StopLoss, bar.Date);
                    return true;
                }
                if (bar.High >= position.TargetPrice)
                {
                    ev = CloseTrade(position, position.TargetPrice, ExitReason.TakeProfit, bar.Date);
                    return true;
                }
            }
            else
            {
                if (bar.High >= position.StopPrice)
                {
                    ev = CloseTrade(position, position.StopPrice, ExitReason.StopLoss, bar.Date);
                    return true;
                }
                if (bar.Low <= position.TargetPrice)
                {
                    ev = CloseTrade(position, position.TargetPrice, ExitReason.TakeProfit, bar.Date);
                    return true;
                }
            }

            return false;
        }

        private SignalEvent CloseTrade(BacktestPosition position, double exitPrice, ExitReason reason, DateTime exitDate)
        {
            double points = position.Side == OrderSide.Buy
                ? exitPrice - position.EntryPrice
                : position.EntryPrice - exitPrice;

            var commission = _config.CommissionPerSide * position.Quantity * 2;
            var pl = points * _pointValue * position.Quantity - commission;
            _cumulativePL += pl;

            if (_cumulativePL > _peak) _peak = _cumulativePL;
            var dd = _peak - _cumulativePL;
            if (dd > _maxDd) _maxDd = dd;

            var trade = new BacktestTrade
            {
                EntryDate = position.EntryDate,
                ExitDate = exitDate,
                Side = position.Side,
                EntryPrice = position.EntryPrice,
                ExitPrice = exitPrice,
                ExitReason = reason,
                Points = points,
                ProfitLoss = pl,
                Commission = commission,
                CumulativePL = _cumulativePL
            };
            _trades.Add(trade);
            _equityCurve.Add(_cumulativePL);

            return new SignalEvent
            {
                Symbol = _config.Symbol,
                TimeFrame = _config.TimeFrame,
                Date = exitDate,
                Type = MapExit(reason),
                Side = position.Side,
                EntryPrice = position.EntryPrice,
                ExitPrice = exitPrice,
                StopPrice = position.StopPrice,
                TargetPrice = position.TargetPrice,
                Quantity = position.Quantity,
                Reason = reason.ToString(),
                Points = points,
                ProfitLoss = pl,
                Commission = commission,
                CumulativePL = _cumulativePL,
                PositionOpen = false
            };
        }

        private static SignalType MapExit(ExitReason reason) => reason switch
        {
            ExitReason.StopLoss => SignalType.ExitStopLoss,
            ExitReason.TakeProfit => SignalType.ExitTakeProfit,
            ExitReason.StrategySignal => SignalType.ExitReversal,
            ExitReason.DayTradeClose => SignalType.ExitDayTrade,
            _ => SignalType.ExitEndOfData
        };

        public void CalculateMetrics(BacktestResult r)
        {
            r.TotalTrades = _trades.Count;
            r.WinCount = _trades.Count(t => t.ProfitLoss > 0);
            r.LossCount = _trades.Count(t => t.ProfitLoss <= 0);
            r.WinRate = r.TotalTrades > 0 ? (double)r.WinCount / r.TotalTrades : 0;
            r.GrossProfit = _trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss);
            r.GrossLoss = Math.Abs(_trades.Where(t => t.ProfitLoss <= 0).Sum(t => t.ProfitLoss));
            r.NetProfit = _cumulativePL;
            r.ProfitFactor = r.GrossLoss > 0 ? r.GrossProfit / r.GrossLoss : r.GrossProfit > 0 ? double.PositiveInfinity : 0;
            r.AvgWin = r.WinCount > 0 ? r.GrossProfit / r.WinCount : 0;
            r.AvgLoss = r.LossCount > 0 ? r.GrossLoss / r.LossCount : 0;
            r.LargestWin = _trades.Count > 0 ? _trades.Max(t => t.ProfitLoss) : 0;
            r.LargestLoss = _trades.Count > 0 ? _trades.Min(t => t.ProfitLoss) : 0;
            r.TotalCommission = _trades.Sum(t => t.Commission);

            var peakVal = 0.0;
            var maxDrawdown = 0.0;
            foreach (var pl in _equityCurve)
            {
                if (pl > peakVal) peakVal = pl;
                var drawdown = peakVal - pl;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            r.MaxDrawdown = maxDrawdown;
            r.MaxDrawdownPct = peakVal > 0 ? maxDrawdown / peakVal * 100 : 0;

            r.Trades = _trades.ToList();
            r.EquityCurve = _equityCurve.ToList();
        }
    }
}
