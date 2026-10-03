using B3WM.Shared.Models.Backtest;

namespace B3WM.Services.Backtest
{
    public sealed class EntrySignal
    {
        public OrderSide Side { get; set; }
        public int Quantity { get; set; } = 1;
        public string? Reason { get; set; }
        public double StopLossPrice { get; set; }
        public double TakeProfitPrice { get; set; }

        /// <summary>Entrada só é válida com stop e alvo definidos.</summary>
        public bool IsValidEntry => StopLossPrice > 0 && TakeProfitPrice > 0;
    }
}
