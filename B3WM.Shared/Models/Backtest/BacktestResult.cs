using B3WM.Shared.Models;

namespace B3WM.Shared.Models.Backtest
{
    public class BacktestResult
    {
        public string StrategyName { get; set; } = string.Empty;
        public BacktestConfig Config { get; set; } = new();
        public int TotalTrades { get; set; }
        public int WinCount { get; set; }
        public int LossCount { get; set; }
        public double WinRate { get; set; }
        public double GrossProfit { get; set; }
        public double GrossLoss { get; set; }
        public double NetProfit { get; set; }
        public double ProfitFactor { get; set; }
        public double AvgWin { get; set; }
        public double AvgLoss { get; set; }
        public double LargestWin { get; set; }
        public double LargestLoss { get; set; }
        public double MaxDrawdown { get; set; }
        public double MaxDrawdownPct { get; set; }
        public double TotalCommission { get; set; }
        public List<BacktestTrade> Trades { get; set; } = new();
        public List<double> EquityCurve { get; set; } = new();
        public List<StructureStorageItem>? StructureLines { get; set; }
    }
}
