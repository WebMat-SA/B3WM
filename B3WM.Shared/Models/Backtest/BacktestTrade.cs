namespace B3WM.Shared.Models.Backtest
{
    public class BacktestTrade
    {
        public DateTime EntryDate { get; set; }
        public DateTime ExitDate { get; set; }
        public OrderSide Side { get; set; }
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public ExitReason ExitReason { get; set; }
        public double Points { get; set; }
        public double ProfitLoss { get; set; }
        public double Commission { get; set; }
        public double CumulativePL { get; set; }
    }
}
