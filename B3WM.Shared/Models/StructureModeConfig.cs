namespace B3WM.Shared.Models
{
    /// <summary>
    /// Configuração persistida do modo de range intraday por símbolo.
    /// O range manual continua sendo a base/fallback usado pelos modos ATR.
    /// </summary>
    public sealed class StructureModeConfig
    {
        /// <summary>Modo intraday (TFs &lt; 1440).</summary>
        public string Mode { get; set; } = StructureRangeModes.Manual;
        public double ManualRange { get; set; }

        /// <summary>Modo do diário (1440).</summary>
        public string DailyMode { get; set; } = StructureRangeModes.Manual;
        public double DailyManualRange { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
