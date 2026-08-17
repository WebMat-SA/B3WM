namespace B3WM.Shared.Models.ExtremeDetection
{
    /// <summary>
    /// Parâmetros do detector de extremos estruturais.
    /// A maioria é derivada automaticamente do próprio sinal; os campos
    /// nullable usam valores automáticos quando nulos.
    /// </summary>
    public class ExtremeDetectorOptions
    {
        /// <summary>
        /// Escala base (σ inicial da pirâmide multi-escala, em amostras).
        /// Automático (~1.5) quando nulo.
        /// </summary>
        public double? SmoothingLevel { get; set; }

        /// <summary>
        /// Escala máxima (σ máximo da pirâmide, em amostras).
        /// Automático (~N/8) quando nulo.
        /// </summary>
        public double? MaxScale { get; set; }

        /// <summary>
        /// Multiplicador do ruído local estimado: a prominência absoluta de um
        /// extremo precisa ser ≥ <see cref="NoiseSensitivity"/> × ruído local.
        /// </summary>
        public double NoiseSensitivity { get; set; } = 3.0;

        /// <summary>
        /// Prominência relativa mínima (razão entre a prominência e a linha de
        /// base local dos pés). Um extremo precisa superar esse valor para ser
        /// considerado significativo. Calibrado empiricamente: estruturas
        /// relevantes de perfis de volume costumam ficar entre 0.15 e 0.35.
        /// </summary>
        public double MinimumProminence { get; set; } = 0.15;

        /// <summary>
        /// Distância mínima entre extremos consecutivos de estruturas
        /// diferentes (em amostras). Automático (~2×escala) quando nulo.
        /// </summary>
        public double? MinimumDistance { get; set; }

        /// <summary>
        /// Margem de borda (em amostras): extremos sem evidência suficiente
        /// nessa margem são classificados como <see cref="ExtremeType.Indeterminate"/>.
        /// Automático (~escala) quando nulo.
        /// </summary>
        public double? EdgeMargin { get; set; }

        /// <summary>Cria uma cópia com os valores automáticos resolvidos.</summary>
        public ExtremeDetectorOptions Clone() => (ExtremeDetectorOptions)MemberwiseClone();
    }
}