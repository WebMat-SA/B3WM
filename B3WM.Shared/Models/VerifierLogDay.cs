namespace B3WM.Shared.Models
{
    /// <summary>Log diário do verificador, salvo em {Symbol}_Verifier_{TF}MIN_{yyyy-MM-dd}.json</summary>
    public class VerifierLogDay
    {
        public string Symbol { get; set; } = string.Empty;
        public int TimeFrame { get; set; }
        public DateTime Date { get; set; }
        public VerifierConfig? Config { get; set; }
        public List<SignalEvent> Signals { get; set; } = new();
        public int TotalTrades { get; set; }
        public int WinCount { get; set; }
        public int LossCount { get; set; }
        public double WinRate { get; set; }
        public double NetProfit { get; set; }
        public double GrossProfit { get; set; }
        public double GrossLoss { get; set; }
        public double MaxDrawdown { get; set; }
        public List<double> EquityCurve { get; set; } = new();
    }
}
