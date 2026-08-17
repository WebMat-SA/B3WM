namespace B3WM.Shared.Models.ExtremeDetection
{
    /// <summary>
    /// Extremo estrutural (topo ou vale) significativo da curva.
    /// Representa a posição representativa de uma estrutura em forma de sino,
    /// não apenas um máximo/mínimo local matemático.
    /// </summary>
    public class ExtremePoint
    {
        /// <summary>Posição no eixo X (ex.: Price).</summary>
        public double Position { get; set; }

        /// <summary>Valor no eixo Y na posição representativa (ex.: Total).</summary>
        public double Value { get; set; }

        /// <summary>Tipo do extremo.</summary>
        public ExtremeType Type { get; set; }

        /// <summary>
        /// Importância relativa (prominência normalizada pela linha de base
        /// local). Valores maiores = estrutura mais relevante em relação ao
        /// comportamento local do sinal.
        /// </summary>
        public double Strength { get; set; }

        /// <summary>Prominência absoluta (diferença de valor em relação ao "pé" mais próximo).</summary>
        public double Prominence { get; set; }

        /// <summary>Escala da estrutura (σ da suavização em que ela sobreviveu).</summary>
        public double Scale { get; set; }

        /// <summary>Confiança (0..1) combinando persistência, prominência e coerência do sino.</summary>
        public double Confidence { get; set; }

        /// <summary>Largura da estrutura (pé esquerdo → pé direito) no eixo X.</summary>
        public double Width { get; set; }

        /// <summary>Posição do pé esquerdo da estrutura.</summary>
        public double StartPosition { get; set; }

        /// <summary>Posição do pé direito da estrutura.</summary>
        public double EndPosition { get; set; }

        /// <summary>
        /// True quando o extremo está na borda da série (sem evidência em um
        /// dos lados). Normalmente acompanhado de <see cref="ExtremeType.Indeterminate"/>.
        /// </summary>
        public bool IsEdge { get; set; }

        public override string ToString()
        {
            return $"{Type} | Pos: {Position} | Value: {Value} | Prom: {Prominence:F0} | " +
                   $"Scale: {Scale:F1} | Strength: {Strength:F2} | Conf: {Confidence:F2}";
        }
    }
}