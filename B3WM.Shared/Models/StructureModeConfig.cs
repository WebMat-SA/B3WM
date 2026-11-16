namespace B3WM.Shared.Models
{
    /// <summary>
    /// Configuração persistida do modo de range intraday por símbolo.
    /// O range manual continua sendo a base/fallback usado pelos modos ATR.
    /// </summary>
    public sealed class StructureModeConfig
    {
        public string Mode { get; set; } = StructureRangeModes.Manual;
        public double ManualRange { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
