namespace B3WM.Shared.Models
{
    public static class Defaults
    {
        public const string Url = "https://localhost:5002/api/datahub";
        public static readonly int[] TimeFrames = [1, 2, 5, 15, 30, 60];

        public static class Symbols
        {
            public const string WINFUT = "WINFUT";
            public const string WDOFUT = "WDOFUT";
        }

        public static class WINFUT
        {
            public const int ThresholdBubbleSize = 250;
            public const double MinDistanceUpdateBorder = 250;
            public const double TickSize = 5.0;
            public const double PointValue = 1.0;
        }

        public static class WDOFUT
        {
            public const int ThresholdBubbleSize = 500;
            public const double MinDistanceUpdateBorder = 2.5;
            public const double TickSize = 0.5;
            public const double PointValue = 10.0;
        }

        public static class Backtest
        {
            public const int SmartEntryThreshold = 500;
            public const int SmartExitThreshold = 1000;
            public const double SmartVolumePct = 0.3;
            public const double SmartStructureBufferPct = 0.1;
        }

        public static double GetPointValue(string symbol) => symbol switch
        {
            Symbols.WINFUT => WINFUT.PointValue,
            Symbols.WDOFUT => WDOFUT.PointValue,
            _ => 1.0
        };

        public static double GetTickSize(string symbol) => symbol switch
        {
            Symbols.WINFUT => WINFUT.TickSize,
            Symbols.WDOFUT => WDOFUT.TickSize,
            _ => 1.0
        };

        public static double GetMinDistance(string symbol) => symbol switch
        {
            Symbols.WINFUT => WINFUT.MinDistanceUpdateBorder,
            Symbols.WDOFUT => WDOFUT.MinDistanceUpdateBorder,
            _ => 250
        };
    }
}
