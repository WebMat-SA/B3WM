using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using B3WM.Services.Core;

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

    public class BacktestEngine
    {
        private readonly DataKeeperBase _dataKeeper;

        public BacktestEngine(DataKeeperBase dataKeeper) => _dataKeeper = dataKeeper;

        private static double GetPointValue(string symbol) => symbol switch
        {
            "WINFUT" => 1.0,
            "WDOFUT" => 10.0,
            _ => 1.0
        };

        public async Task<BacktestResult> Run(BacktestConfig config, IStrategy strategy)
        {
            await strategy.InitializeAsync();

            var bars = await LoadBars(config);
            var result = new BacktestResult
            {
                StrategyName = strategy.Name,
                Config = config
            };

            if (bars.Count == 0) return result;

            var pointValue = GetPointValue(config.Symbol);
            var trades = new List<BacktestTrade>();
            var equityCurve = new List<double> { 0 };
            BacktestPosition? position = null;
            double cumulativePL = 0;
            double peak = 0;
            double maxDd = 0;

            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];

                bool closed = false;
                if (position != null)
                {
                    TryCloseByPrice(bar, position, config, pointValue, ref cumulativePL, ref peak, ref maxDd, trades, equityCurve, out closed);
                    if (closed) position = null;
                }

                if (position != null && !closed)
                {
                    var signal = strategy.Evaluate(bar, hasPosition: true);
                    if (signal != null)
                    {
                        CloseTrade(position, bar.Close, ExitReason.StrategySignal, bar.Date, pointValue, config, ref cumulativePL, ref peak, ref maxDd, trades, equityCurve);
                        position = null;
                    }
                }

                if (position == null)
                {
                    var signal = strategy.Evaluate(bar, hasPosition: false);
                    if (signal != null)
                    {
                        var entryPrice = signal.Side == OrderSide.Buy
                            ? bar.Close + config.SlippagePoints
                            : bar.Close - config.SlippagePoints;

                        var stopPrice = signal.StopLossPrice ?? (signal.Side == OrderSide.Buy
                            ? entryPrice - config.StopLossPoints
                            : entryPrice + config.StopLossPoints);

                        var targetPrice = signal.TakeProfitPrice ?? (signal.Side == OrderSide.Buy
                            ? entryPrice + config.TakeProfitPoints
                            : entryPrice - config.TakeProfitPoints);

                        position = new BacktestPosition
                        {
                            Side = signal.Side,
                            EntryPrice = entryPrice,
                            StopPrice = stopPrice,
                            TargetPrice = targetPrice,
                            Quantity = signal.Quantity > 0 ? signal.Quantity : config.Quantity,
                            EntryDate = bar.Date,
                            EntryReason = signal.Reason
                        };
                    }
                }

                if (position != null && i == bars.Count - 1)
                {
                    CloseTrade(position, bar.Close, ExitReason.EndOfData, bar.Date, pointValue, config, ref cumulativePL, ref peak, ref maxDd, trades, equityCurve);
                    position = null;
                }
            }

            CalculateMetrics(result, trades, equityCurve, cumulativePL);
            return result;
        }

        private async Task<List<BarStorageItem>> LoadBars(BacktestConfig config)
        {
            var allBars = new List<BarStorageItem>();
            var current = config.StartDate.Date;

            while (current <= config.EndDate.Date)
            {
                var path = $"{config.Symbol}_{nameof(CandleService)}_{config.TimeFrame}MIN_{current:yyyy-MM-dd}.json";
                try
                {
                    var dayBars = await _dataKeeper.ReadDataAsync<List<BarStorageItem>>(path);
                    allBars.AddRange(dayBars);
                }
                catch { }
                current = current.AddDays(1);
            }

            return allBars
                .Where(b => b.Date >= config.StartDate && b.Date <= config.EndDate)
                .OrderBy(b => b.Date)
                .ToList();
        }

        private static void TryCloseByPrice(
            BarStorageItem bar,
            BacktestPosition position,
            BacktestConfig config,
            double pointValue,
            ref double cumulativePL,
            ref double peak,
            ref double maxDd,
            List<BacktestTrade> trades,
            List<double> equityCurve,
            out bool closed)
        {
            closed = false;

            if (position.Side == OrderSide.Buy)
            {
                if (bar.Low <= position.StopPrice)
                {
                    var exitPrice = position.StopPrice - config.SlippagePoints;
                    CloseTrade(position, exitPrice, ExitReason.StopLoss, bar.Date, pointValue, config, ref cumulativePL, ref peak, ref maxDd, trades, equityCurve);
                    closed = true;
                    return;
                }
                if (bar.High >= position.TargetPrice)
                {
                    var exitPrice = position.TargetPrice - config.SlippagePoints;
                    CloseTrade(position, exitPrice, ExitReason.TakeProfit, bar.Date, pointValue, config, ref cumulativePL, ref peak, ref maxDd, trades, equityCurve);
                    closed = true;
                }
            }
            else
            {
                if (bar.High >= position.StopPrice)
                {
                    var exitPrice = position.StopPrice + config.SlippagePoints;
                    CloseTrade(position, exitPrice, ExitReason.StopLoss, bar.Date, pointValue, config, ref cumulativePL, ref peak, ref maxDd, trades, equityCurve);
                    closed = true;
                    return;
                }
                if (bar.Low <= position.TargetPrice)
                {
                    var exitPrice = position.TargetPrice + config.SlippagePoints;
                    CloseTrade(position, exitPrice, ExitReason.TakeProfit, bar.Date, pointValue, config, ref cumulativePL, ref peak, ref maxDd, trades, equityCurve);
                    closed = true;
                }
            }
        }

        private static void CloseTrade(
            BacktestPosition position,
            double exitPrice,
            ExitReason reason,
            DateTime exitDate,
            double pointValue,
            BacktestConfig config,
            ref double cumulativePL,
            ref double peak,
            ref double maxDd,
            List<BacktestTrade> trades,
            List<double> equityCurve)
        {
            double points = position.Side == OrderSide.Buy
                ? exitPrice - position.EntryPrice
                : position.EntryPrice - exitPrice;

            var commission = config.CommissionPerSide * position.Quantity * 2;
            var pl = points * pointValue * position.Quantity - commission;
            cumulativePL += pl;

            if (cumulativePL > peak) peak = cumulativePL;
            var dd = peak - cumulativePL;
            if (dd > maxDd) maxDd = dd;

            trades.Add(new BacktestTrade
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
                CumulativePL = cumulativePL
            });

            equityCurve.Add(cumulativePL);
        }

        private static void CalculateMetrics(BacktestResult r, List<BacktestTrade> trades, List<double> equityCurve, double finalPL)
        {
            r.TotalTrades = trades.Count;
            r.WinCount = trades.Count(t => t.ProfitLoss > 0);
            r.LossCount = trades.Count(t => t.ProfitLoss <= 0);
            r.WinRate = r.TotalTrades > 0 ? (double)r.WinCount / r.TotalTrades : 0;
            r.GrossProfit = trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss);
            r.GrossLoss = Math.Abs(trades.Where(t => t.ProfitLoss <= 0).Sum(t => t.ProfitLoss));
            r.NetProfit = finalPL;
            r.ProfitFactor = r.GrossLoss > 0 ? r.GrossProfit / r.GrossLoss : r.GrossProfit > 0 ? double.PositiveInfinity : 0;
            r.AvgWin = r.WinCount > 0 ? r.GrossProfit / r.WinCount : 0;
            r.AvgLoss = r.LossCount > 0 ? r.GrossLoss / r.LossCount : 0;
            r.LargestWin = trades.Count > 0 ? trades.Max(t => t.ProfitLoss) : 0;
            r.LargestLoss = trades.Count > 0 ? trades.Min(t => t.ProfitLoss) : 0;
            r.TotalCommission = trades.Sum(t => t.Commission);

            var maxDdTrade = trades.OrderByDescending(t => t.CumulativePL - trades.Where(x => x.CumulativePL <= t.CumulativePL).DefaultIfEmpty(t).Min(x => x.CumulativePL)).FirstOrDefault();
            r.MaxDrawdown = maxDdTrade != null ? equityCurve.DefaultIfEmpty(0).Max() - equityCurve.DefaultIfEmpty(0).Min() : 0;

            var peakVal = 0.0;
            var maxDrawdown = 0.0;
            foreach (var pl in equityCurve)
            {
                if (pl > peakVal) peakVal = pl;
                var drawdown = peakVal - pl;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            r.MaxDrawdown = maxDrawdown;
            r.MaxDrawdownPct = peakVal > 0 ? maxDrawdown / peakVal * 100 : 0;

            r.Trades = trades;
            r.EquityCurve = equityCurve;
        }
    }
}
