namespace B3WM.Services.Research
{
    /// <summary>
    /// Configuração do experimento de range adaptativo (issue #25).
    /// Default desligado: o estudo roda em paralelo, sem tocar na estrutura
    /// de produção, e grava apenas em Data/Research.
    /// </summary>
    public sealed class StructureResearchOptions
    {
        public bool Enabled { get; set; }

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

        /// <summary>Dias de candles reprocessados no startup para aquecer ATR/estrutura.</summary>
        public int WarmupDays { get; set; } = 3;

        public int FalseBreakBarsShort { get; set; } = 10;
        public int FalseBreakBarsLong { get; set; } = 20;

        public int[] TimeFrames { get; set; } = [1, 2, 5, 15, 30, 60];
        public string[] Symbols { get; set; } = ["WINFUT", "WDOFUT"];

        public bool Persist { get; set; } = true;

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
