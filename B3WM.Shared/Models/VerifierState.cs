using B3WM.Shared.Models.Backtest;

namespace B3WM.Shared.Models
{
    public class VerifierPosition
    {
        public OrderSide Side { get; set; }
        public double EntryPrice { get; set; }
        public double StopPrice { get; set; }
        public double TargetPrice { get; set; }
        public int Quantity { get; set; }
        public DateTime EntryDate { get; set; }
        public string? EntryReason { get; set; }
    }

    public class VerifierPendingSignal
    {
        public OrderSide Side { get; set; }
        public string? Reason { get; set; }
        public double StopLossPrice { get; set; }
        public double TakeProfitPrice { get; set; }
    }

    public class VerifierState
    {
        public string Symbol { get; set; } = string.Empty;
        public int TimeFrame { get; set; }
        public bool IsRunning { get; set; }
        public VerifierConfig? Config { get; set; }
        public VerifierPosition? OpenPosition { get; set; }
        public VerifierPendingSignal? PendingSignal { get; set; }
        public int TotalTrades { get; set; }
        public int WinCount { get; set; }
        public int LossCount { get; set; }
        public double WinRate { get; set; }
        public double NetProfit { get; set; }
        public double GrossProfit { get; set; }
        public double GrossLoss { get; set; }
        public double MaxDrawdown { get; set; }
        public List<SignalEvent> Signals { get; set; } = new();
        public List<double> EquityCurve { get; set; } = new();
    }
}
