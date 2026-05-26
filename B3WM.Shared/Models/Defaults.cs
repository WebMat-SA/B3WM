using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B3WM.Shared.Models
{
    public static class Defaults
    {
        public const string Url = "https://localhost:5002/api/datahub";
        public static readonly int[] TimeFrames = new int[] { 1, 2, 5, 15, 30, 60 };

        public static class Symbols
        {
            public const string WINFUT = "WINFUT";
            public const string WDOFUT = "WDOFUT";
        }

        public static class WINFUT
        {
            public const int ThresholdBubbleSize = 250;
            public const double MinDistanceUpdateBorder = 250;
        }

        public static class WDOFUT
        {
            public const int ThresholdBubbleSize = 1000;
            public const double MinDistanceUpdateBorder = 2.5;
        }


    }
}
