namespace B3WM.Shared.Models
{
    /// <summary>
    /// Modos do range intraday da estrutura (issue #25).
    /// A string é a chave estável usada na API, no arquivo de config e no app.
    /// </summary>
    public static class StructureRangeModes
    {
        public const string Manual = "manual";
        public const string AtrPerLeg = "atr_leg";
        public const string AtrPerBar = "atr_bar";

        public static readonly string[] All = [Manual, AtrPerLeg, AtrPerBar];

        public static bool IsValid(string? mode) =>
            !string.IsNullOrWhiteSpace(mode) &&
            (mode.Equals(Manual, StringComparison.OrdinalIgnoreCase) ||
             mode.Equals(AtrPerLeg, StringComparison.OrdinalIgnoreCase) ||
             mode.Equals(AtrPerBar, StringComparison.OrdinalIgnoreCase));

        public static string Normalize(string? mode)
        {
            if (mode?.Equals(AtrPerLeg, StringComparison.OrdinalIgnoreCase) == true)
                return AtrPerLeg;
            if (mode?.Equals(AtrPerBar, StringComparison.OrdinalIgnoreCase) == true)
                return AtrPerBar;
            return Manual;
        }
    }
}
