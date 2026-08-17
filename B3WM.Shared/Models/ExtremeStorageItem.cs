using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Shared.Models
{
    /// <summary>
    /// Resultado da detecção de topos/vales estruturais persistido em JSON pelo
    /// servidor e transmitido via SignalR.
    /// </summary>
    public class ExtremeStorageItem
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public string Symbol { get; set; } = string.Empty;

        /// <summary>Início do período do perfil de volume (null = dia inteiro).</summary>
        public DateTime? PeriodFrom { get; set; }

        /// <summary>Fim do período do perfil de volume (null = dia inteiro / fim ao vivo).</summary>
        public DateTime? PeriodTo { get; set; }

        /// <summary>Parâmetros usados na detecção.</summary>
        public ExtremeDetectorOptions Config { get; set; } = new();

        public List<ExtremePoint> Extremes { get; set; } = new();

        public List<BellStructure> Structures { get; set; } = new();

        public ExtremeStatistics Statistics { get; set; } = new();
    }
}