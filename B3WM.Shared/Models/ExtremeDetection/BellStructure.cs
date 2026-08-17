namespace B3WM.Shared.Models.ExtremeDetection
{
    /// <summary>
    /// Estrutura completa em forma de sino: pé esquerdo → subida → topo →
    /// descida → pé direito. Um vale pode servir de pé compartilhado entre
    /// dois sinos adjacentes.
    /// </summary>
    public class BellStructure
    {
        /// <summary>Posição do início da estrutura (pé esquerdo).</summary>
        public double StartPosition { get; set; }

        /// <summary>Posição do fim da estrutura (pé direito).</summary>
        public double EndPosition { get; set; }

        /// <summary>Posição do vale do pé esquerdo (pode ser nulo na borda).</summary>
        public double? ValleyPosition { get; set; }

        /// <summary>Posição do topo da estrutura.</summary>
        public double? TopPosition { get; set; }

        /// <summary>Posição do vale do pé direito (pode ser nulo na borda).</summary>
        public double? NextValleyPosition { get; set; }

        /// <summary>Amplitude (valor do topo − mínimo dos pés).</summary>
        public double Amplitude { get; set; }

        /// <summary>Largura da estrutura no eixo X (pé esquerdo → pé direito).</summary>
        public double Width { get; set; }

        /// <summary>Prominência da estrutura.</summary>
        public double Prominence { get; set; }

        /// <summary>Confiança (0..1).</summary>
        public double Confidence { get; set; }

        /// <summary>Escala da estrutura (σ).</summary>
        public double Scale { get; set; }
    }
}