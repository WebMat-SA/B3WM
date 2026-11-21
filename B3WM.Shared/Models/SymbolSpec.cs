namespace B3WM.Shared.Models
{
    /// <summary>Periodicidade do vencimento do contrato futuro.</summary>
    public enum ContractFrequency
    {
        /// <summary>Meses pares, quarta-feira próxima do dia 15 (WIN/IND).</summary>
        Bimonthly,

        /// <summary>Mensal, primeiro dia útil (WDO/DOL).</summary>
        Monthly,
    }

    /// <summary>
    /// Especificação canônica de um símbolo (nome Profit, root MT5, tick,
    /// ranges e multiplicadores). Fonte única no backend para evitar os
    /// <c>switch</c> repetidos em Defaults/Extensions/ATR (issue #47).
    /// </summary>
    public sealed record SymbolSpec(
        string Name,
        string Mt5Root,
        ContractFrequency Frequency,
        double TickSize,
        double PointValue,
        int ThresholdBubbleSize,
        double MinDistanceUpdateBorder,
        double MinDistanceUpdateBorderDaily);
}
