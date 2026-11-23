namespace B3WM.Shared.Models
{
    public static class Defaults
    {
        public const string Url = "https://localhost:5002/api/datahub";
        public static readonly int[] TimeFrames = [1, 2, 5, 15, 30, 60, 1440];

        public static class Symbols
        {
            public const string WINFUT = "WINFUT";
            public const string INDFUT = "INDFUT";
            public const string WDOFUT = "WDOFUT";
            public const string DOLFUT = "DOLFUT";
        }

        public static class WINFUT
        {
            public const int ThresholdBubbleSize = 250;
            public const double MinDistanceUpdateBorder = 250;
            public const double MinDistanceUpdateBorderDaily = 1000;
            public const double TickSize = 5.0;
            public const double PointValue = 1.0;
        }

        // Contrato cheio do índice: mesmo ativo-objeto do WIN (preço/pontos
        // idênticos). O lote mínimo é 5 contratos e a liquidez é muito menor
        // que a do mini, então o threshold de detecção fica em 10 lotes (50),
        // calibrado pelos volumes reais (arquivos Data/INDFUT_VolumeService).
        public static class INDFUT
        {
            public const int ThresholdBubbleSize = 50;
            public const double MinDistanceUpdateBorder = 250;
            public const double MinDistanceUpdateBorderDaily = 1000;
            public const double TickSize = 5.0;
            public const double PointValue = 1.0;
        }

        public static class WDOFUT
        {
            public const int ThresholdBubbleSize = 500;
            public const double MinDistanceUpdateBorder = 2.5;
            public const double MinDistanceUpdateBorderDaily = 100;
            public const double TickSize = 0.5;
            public const double PointValue = 10.0;
        }

        // Contrato cheio do dólar: mesmo preço/pontos do WDO, com valor do
        // tick 5x. O lote mínimo é 5 contratos; 50 (10 lotes) acompanha a
        // liquidez real do DOL (série ~10x menor que o WDO).
        public static class DOLFUT
        {
            public const int ThresholdBubbleSize = 50;
            public const double MinDistanceUpdateBorder = 2.5;
            public const double MinDistanceUpdateBorderDaily = 100;
            public const double TickSize = 0.5;
            public const double PointValue = 50.0;
        }

        public static class Backtest
        {
            public const int SmartEntryThreshold = 500;
            public const int SmartExitThreshold = 1000;
            public const double SmartVolumePct = 0.3;
            public const double SmartStructureBufferPct = 0.1;
        }

        public static class Extreme
        {
            public const double NoiseSensitivity = 3.0;
            public const double MinimumProminence = 0.15;
            public const double SmoothingLevel = 1.5;

            // Limites para os sliders do app
            public const double NoiseSensitivityMin = 1.0;
            public const double NoiseSensitivityMax = 10.0;
            public const double NoiseSensitivityStep = 0.5;
            public const double MinimumProminenceMin = 0.05;
            public const double MinimumProminenceMax = 0.5;
            public const double MinimumProminenceStep = 0.05;
        }

        public static double GetPointValue(string symbol) =>
            SymbolRegistry.Get(symbol).PointValue;

        public static double GetTickSize(string symbol) =>
            SymbolRegistry.Get(symbol).TickSize;

        public static int GetThresholdBubble(string symbol) =>
            SymbolRegistry.Get(symbol).ThresholdBubbleSize;

        public static double GetMinDistance(string symbol) =>
            SymbolRegistry.Get(symbol).MinDistanceUpdateBorder;

        public static double GetMinDistanceDaily(string symbol) =>
            SymbolRegistry.Get(symbol).MinDistanceUpdateBorderDaily;
    }
}
