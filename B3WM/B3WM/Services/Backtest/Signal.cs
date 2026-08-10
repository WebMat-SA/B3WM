using B3WM.Shared.Models.Backtest;

namespace B3WM.Services.Backtest
{
    public class Signal
    {
        public OrderSide Side { get; set; }
        public int Quantity { get; set; } = 1;
        public string? Reason { get; set; }
        public double StopLossPrice { get; set; }
        public double TakeProfitPrice { get; set; }

        /// <summary>
        /// Um sinal de ENTRADA só é válido se definir stop loss e take profit
        /// (compõem a própria info do sinal, junto com os motivos de entrada).
        /// Sinais de saída (reversão/close) não precisam preenchê-los.
        /// </summary>
        public bool IsValidEntry => StopLossPrice > 0 && TakeProfitPrice > 0;
    }
}
