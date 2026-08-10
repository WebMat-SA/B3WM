using B3WM.Shared.Models.Backtest;

namespace B3WM.Shared.Models
{
    public enum SignalType
    {
        EntryBuy,
        EntrySell,
        ExitStopLoss,
        ExitTakeProfit,
        ExitReversal,
        ExitDayTrade,
        ExitEndOfData
    }

    public class SignalEvent
    {
        public string Symbol { get; set; } = string.Empty;
        public int TimeFrame { get; set; }
        public DateTime Date { get; set; }
        public SignalType Type { get; set; }
        public OrderSide Side { get; set; }
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public double StopPrice { get; set; }
        public double TargetPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public string? Reason { get; set; }
        public double Points { get; set; }
        public double ProfitLoss { get; set; }
        public double Commission { get; set; }
        public double CumulativePL { get; set; }
        public bool PositionOpen { get; set; }
    }
}
