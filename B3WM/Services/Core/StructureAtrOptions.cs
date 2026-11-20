namespace B3WM.Services.Core
{
    /// <summary>
    /// Parâmetros do range adaptativo por ATR usado pela estrutura (issue #25).
    /// </summary>
    public sealed class StructureAtrOptions
    {
        /// <summary>Período do ATR (média simples dos true ranges).</summary>
        public int AtrPeriod { get; set; } = 14;

        public double AtrMultiplierWinfut { get; set; } = 1.5;
        public double AtrMultiplierWdofut { get; set; } = 1.0;

        /// <summary>
        /// Multiplicadores do diário (1440). A ordem de grandeza é outra:
        /// WINFUT ~0,3 ATR e WDOFUT ~1,0 ATR aproximam os ranges manuais
        /// históricos (1000 / 100).
        /// </summary>
        public double AtrDailyMultiplierWinfut { get; set; } = 0.3;
        public double AtrDailyMultiplierWdofut { get; set; } = 1.0;

        /// <summary>Piso do range intraday em ticks.</summary>
        public double AtrFloorTicks { get; set; } = 5;

        /// <summary>Teto do range intraday em ticks.</summary>
        public double AtrCapTicks { get; set; } = 400;

        /// <summary>Piso do range diário (1440) em ticks.</summary>
        public double AtrDailyFloorTicks { get; set; } = 20;

        /// <summary>Teto do range diário (1440) em ticks.</summary>
        public double AtrDailyCapTicks { get; set; } = 2000;

        public double MultiplierFor(string symbol) =>
            symbol.Equals("WINFUT", StringComparison.OrdinalIgnoreCase)
                ? AtrMultiplierWinfut
                : AtrMultiplierWdofut;

        public double DailyMultiplierFor(string symbol) =>
            symbol.Equals("WINFUT", StringComparison.OrdinalIgnoreCase)
                ? AtrDailyMultiplierWinfut
                : AtrDailyMultiplierWdofut;
    }
}
