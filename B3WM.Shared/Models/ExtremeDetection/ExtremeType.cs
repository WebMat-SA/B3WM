namespace B3WM.Shared.Models.ExtremeDetection
{
    /// <summary>
    /// Tipo do extremo estrutural detectado.
    /// </summary>
    public enum ExtremeType
    {
        /// <summary>Topo (máximo estrutural) confirmado.</summary>
        Top,

        /// <summary>Vale (mínimo estrutural) confirmado.</summary>
        Valley,

        /// <summary>
        /// Extremo sem evidência suficiente (ex.: localizado na borda da série,
        /// sem subida/descida confirmável em um dos lados).
        /// </summary>
        Indeterminate
    }
}