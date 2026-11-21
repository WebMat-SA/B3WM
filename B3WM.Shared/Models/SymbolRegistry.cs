namespace B3WM.Shared.Models
{
    /// <summary>
    /// Registro central dos símbolos operados (issue #47). Adicionar um símbolo
    /// novo = uma entrada aqui + classe de constantes em <see cref="Defaults"/>;
    /// o DI, o backfill e os getters de <see cref="Defaults"/> derivam daqui.
    /// </summary>
    public static class SymbolRegistry
    {
        public static readonly SymbolSpec Winfut = new(
            Defaults.Symbols.WINFUT,
            "WIN",
            ContractFrequency.Bimonthly,
            Defaults.WINFUT.TickSize,
            Defaults.WINFUT.PointValue,
            Defaults.WINFUT.ThresholdBubbleSize,
            Defaults.WINFUT.MinDistanceUpdateBorder,
            Defaults.WINFUT.MinDistanceUpdateBorderDaily);

        public static readonly SymbolSpec Indfut = new(
            Defaults.Symbols.INDFUT,
            "IND",
            ContractFrequency.Bimonthly,
            Defaults.INDFUT.TickSize,
            Defaults.INDFUT.PointValue,
            Defaults.INDFUT.ThresholdBubbleSize,
            Defaults.INDFUT.MinDistanceUpdateBorder,
            Defaults.INDFUT.MinDistanceUpdateBorderDaily);

        public static readonly SymbolSpec Wdofut = new(
            Defaults.Symbols.WDOFUT,
            "WDO",
            ContractFrequency.Monthly,
            Defaults.WDOFUT.TickSize,
            Defaults.WDOFUT.PointValue,
            Defaults.WDOFUT.ThresholdBubbleSize,
            Defaults.WDOFUT.MinDistanceUpdateBorder,
            Defaults.WDOFUT.MinDistanceUpdateBorderDaily);

        public static readonly SymbolSpec Dolfut = new(
            Defaults.Symbols.DOLFUT,
            "DOL",
            ContractFrequency.Monthly,
            Defaults.DOLFUT.TickSize,
            Defaults.DOLFUT.PointValue,
            Defaults.DOLFUT.ThresholdBubbleSize,
            Defaults.DOLFUT.MinDistanceUpdateBorder,
            Defaults.DOLFUT.MinDistanceUpdateBorderDaily);

        /// <summary>Ordem exibida no seletor: índice mini/cheio, dólar mini/cheio.</summary>
        public static readonly IReadOnlyList<SymbolSpec> All = [Winfut, Indfut, Wdofut, Dolfut];

        public static readonly IReadOnlyList<string> Names =
            All.Select(s => s.Name).ToArray();

        private static readonly Dictionary<string, SymbolSpec> ByName =
            All.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        // Mantém o fallback histórico dos switches de Defaults para símbolos
        // desconhecidos (não quebra consumidores que passam nome livre).
        private static readonly SymbolSpec Fallback = new(
            string.Empty,
            string.Empty,
            ContractFrequency.Monthly,
            TickSize: 1.0,
            PointValue: 1.0,
            ThresholdBubbleSize: 250,
            MinDistanceUpdateBorder: 250,
            MinDistanceUpdateBorderDaily: 1000);

        public static SymbolSpec Get(string? symbol) =>
            TryGet(symbol, out var spec) ? spec : Fallback;

        public static bool TryGet(string? symbol, out SymbolSpec spec)
        {
            if (!string.IsNullOrWhiteSpace(symbol) &&
                ByName.TryGetValue(symbol.Trim(), out var found))
            {
                spec = found;
                return true;
            }

            spec = Fallback;
            return false;
        }

        public static bool IsKnown(string? symbol) => TryGet(symbol, out _);
    }
}
