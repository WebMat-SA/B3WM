using B3WM.Shared.Models.Backtest;

namespace B3WM.Services.Backtest
{
    public class Signal
    {
        public OrderSide Side { get; set; }
        public int Quantity { get; set; } = 1;
        public string? Reason { get; set; }
        public double? StopLossPrice { get; set; }
        public double? TakeProfitPrice { get; set; }
    }
}
