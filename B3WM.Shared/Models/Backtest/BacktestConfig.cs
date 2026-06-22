namespace B3WM.Shared.Models.Backtest
{
    public class BacktestConfig
    {
        public string Symbol { get; set; } = "WINFUT";
        public int TimeFrame { get; set; } = 5;
        public StrategyType StrategyName { get; set; } = StrategyType.Breakout;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double StopLossPoints { get; set; } = 200;
        public double TakeProfitPoints { get; set; } = 400;
        public int Quantity { get; set; } = 1;
        public double SlippagePoints { get; set; } = 0;
        public double CommissionPerSide { get; set; } = 0.90;
        public int LookbackPeriod { get; set; } = 20;
    }
}
