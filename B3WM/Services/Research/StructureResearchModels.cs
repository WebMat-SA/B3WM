namespace B3WM.Services.Research
{
    /// <summary>Chaves das séries comparadas no experimento.</summary>
    public static class StructureResearchVariants
    {
        public const string Manual = "manual";
        public const string AtrPerLeg = "atr_leg";
        public const string AtrPerBar = "atr_bar";

        public static readonly string[] All = [Manual, AtrPerLeg, AtrPerBar];
    }

    /// <summary>
    /// Um candle processado por uma variante. DTO próprio (não mexe no
    /// StructureStorageItem de produção) para guardar range/ATR usados.
    /// </summary>
    public sealed class StructureResearchPoint
    {
        public DateTime Date { get; set; }
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; }
        public string Variant { get; set; } = "";

        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }

        public double UpBorder { get; set; }
        public double DownBorder { get; set; }
        public double UpAuxBorder { get; set; }
        public double DownAuxBorder { get; set; }

        /// <summary>Range efetivo usado neste candle (pontos).</summary>
        public double RangeUsed { get; set; }

        /// <summary>ATR no momento do candle (pontos).</summary>
        public double Atr { get; set; }

        /// <summary>ATR no momento do candle (ticks).</summary>
        public double AtrTicks { get; set; }

        public bool UpChanged { get; set; }
        public bool DownChanged { get; set; }
    }

    public sealed class StructureResearchVariantReport
    {
        public string Variant { get; set; } = "";
        public int Bars { get; set; }
        public int UpChanges { get; set; }
        public int DownChanges { get; set; }
        public int TotalChanges { get; set; }
        public double AvgLegBars { get; set; }
        public double MedianLegBars { get; set; }

        public double RangeMin { get; set; }
        public double RangeAvg { get; set; }
        public double RangeMax { get; set; }

        public double AtrMin { get; set; }
        public double AtrAvg { get; set; }
        public double AtrMax { get; set; }

        public int FalseBreaksShort { get; set; }
        public int FalseBreaksLong { get; set; }
        public double FalseBreakRateShort { get; set; }
        public double FalseBreakRateLong { get; set; }

        public double FollowThroughAtrAvg { get; set; }

        /// <summary>Mudanças desta variante que não existem no baseline manual.</summary>
        public int ChangesNotMatchedManual { get; set; }
    }

    public sealed class StructureResearchReport
    {
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; }
        public DateTime Date { get; set; }
        public DateTime GeneratedAt { get; set; }

        public int AtrPeriod { get; set; }
        public double AtrMultiplier { get; set; }
        public double ManualRange { get; set; }

        public List<StructureResearchVariantReport> Variants { get; set; } = new();
    }
}
