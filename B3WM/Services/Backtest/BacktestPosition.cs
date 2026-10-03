using B3WM.Shared.Models.Backtest;

namespace B3WM.Services.Backtest
{
    public class BacktestPosition
    {
        public OrderSide Side { get; set; }
        public double EntryPrice { get; set; }
        public double StopPrice { get; set; }
        public double TargetPrice { get; set; }
        public int Quantity { get; set; }
        public DateTime EntryDate { get; set; }
        public string? EntryReason { get; set; }
    }
}
