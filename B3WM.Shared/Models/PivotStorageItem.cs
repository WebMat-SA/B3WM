namespace B3WM.Shared.Models
{
    /// <summary>
    /// Nível de Pivot Tradicional (Profit): P, R1..R5, S1..S5.
    /// Serializado em camelCase para o Flutter (levels[].key/value).
    /// </summary>
    public class PivotLevel
    {
        public string Key { get; set; } = "";
        public double Value { get; set; }
    }

    /// <summary>
    /// Snapshot estático do Pivot do dia (só dia atual/último pregão).
    /// Intraday: fonte HLC de D-1. Daily: fonte HLC da semana anterior.
    /// Função pura — sem estado ao vivo, sem SignalR.
    /// </summary>
    public class PivotStorageItem
    {
        public string Symbol { get; set; } = "";
        /// <summary>Sessão exibida (dia atual ou último pregão com dados).</summary>
        public DateTime Date { get; set; }
        /// <summary>"D-1" (intraday) ou "W-1" (daily).</summary>
        public string Source { get; set; } = "";
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public int LineCount { get; set; } = 2;
        public List<PivotLevel> Levels { get; set; } = new();
    }
}
