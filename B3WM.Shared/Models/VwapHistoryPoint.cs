namespace B3WM.Shared.Models
{
    /// <summary>
    /// Um ponto do histórico VWAP por minuto (fonte única: typical HLC/3 dos candles 1MIN).
    /// Persistido como lista do dia em {SYM}_VwapService_yyyy-MM-dd.json.
    /// </summary>
    public class VwapHistoryPoint
    {
        /// <summary>Início do minuto (== Date do candle 1MIN fechado).</summary>
        public DateTime Minute { get; set; }

        public double Vwap { get; set; }
        public double SumPv { get; set; }
        public long SumV { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public DateTime Day { get; set; }
    }
}
