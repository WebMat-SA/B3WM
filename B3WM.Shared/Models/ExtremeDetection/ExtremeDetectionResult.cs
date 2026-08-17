namespace B3WM.Shared.Models.ExtremeDetection
{
    /// <summary>Estatísticas estimadas da série de entrada.</summary>
    public class ExtremeStatistics
    {
        public int PointCount { get; set; }

        /// <summary>Espaçamento médio do eixo X (usado para converter amostras em unidades de X).</summary>
        public double Spacing { get; set; }

        public double MinX { get; set; }
        public double MaxX { get; set; }

        /// <summary>Desvio padrão robusto do ruído local estimado.</summary>
        public double NoiseStd { get; set; }

        /// <summary>Escala base usada (σ inicial).</summary>
        public double BaseScale { get; set; }

        /// <summary>Escala máxima usada (σ final).</summary>
        public double MaxScale { get; set; }

        /// <summary>Número de topos confirmados.</summary>
        public int TopCount { get; set; }

        /// <summary>Número de vales confirmados.</summary>
        public int ValleyCount { get; set; }

        /// <summary>Número de extremos indeterminados (borda).</summary>
        public int IndeterminateCount { get; set; }
    }

    /// <summary>
    /// Resultado rico da detecção: extremos (confirmados + indeterminados),
    /// estruturas em forma de sino e estatísticas da série.
    /// </summary>
    public class ExtremeDetectionResult
    {
        /// <summary>Todos os extremos detectados (inclui indeterminados de borda).</summary>
        public List<ExtremePoint> Extremes { get; set; } = new();

        /// <summary>Topos confirmados.</summary>
        public List<ExtremePoint> Tops => Extremes
            .Where(e => e.Type == ExtremeType.Top)
            .OrderBy(e => e.Position)
            .ToList();

        /// <summary>Vales confirmados.</summary>
        public List<ExtremePoint> Valleys => Extremes
            .Where(e => e.Type == ExtremeType.Valley)
            .OrderBy(e => e.Position)
            .ToList();

        /// <summary>Estruturas em forma de sino confirmadas.</summary>
        public List<BellStructure> Structures { get; set; } = new();

        public ExtremeStatistics Statistics { get; set; } = new();
    }
}