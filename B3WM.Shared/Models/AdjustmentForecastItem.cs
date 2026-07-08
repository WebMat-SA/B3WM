namespace B3WM.Shared.Models
{
    public class AdjustmentForecastItem
    {
        public double Vwap { get; set; }
        public DateTime Time { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public double SumPv { get; set; }
        public long SumV { get; set; }
        public DateTime Day { get; set; }
    }
}
