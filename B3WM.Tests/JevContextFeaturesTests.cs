using B3WM.Services.AI;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Tests;

/// <summary>
/// Features puras de contexto da Jev: volume profile, movimento recente,
/// zonas com bolhas filtradas e a regra objetiva de confluência.
/// </summary>
public class JevContextFeaturesTests
{
    private static readonly DateTime Day = new(2026, 11, 12, 10, 0, 0);

    private static BarStorageItem Bar(DateTime date, double open, double high,
        double low, double close, int tf = 2) => new()
    {
        Date = date,
        Symbol = "WINFUT",
        TimeFrame = tf,
        Open = open,
        High = high,
        Low = low,
        Close = close,
        Volume = 1,
    };

    [Fact]
    public void SummarizeVolume_ComputesPocValueAreaAndNodes()
    {
        var item = new VolumeLevelStorageItem
        {
            Symbol = "WINFUT",
            Volumes = new List<VolumeLevel>
            {
                new() { Price = 100, Total = 10, BuyVolume = 6, SellVolume = 4 },
                new() { Price = 101, Total = 30, BuyVolume = 18, SellVolume = 12 },
                new() { Price = 102, Total = 60, BuyVolume = 36, SellVolume = 24 },
                new() { Price = 103, Total = 30, BuyVolume = 18, SellVolume = 12 },
                new() { Price = 104, Total = 10, BuyVolume = 6, SellVolume = 4 },
            },
        };
        var options = JevFeatureOptions.Default with
        {
            LowVolumeRank = 0.50,
            VolumeWindowTicks = 1,
        };

        var summary = JevContextAnalyzer.SummarizeVolume(item, 100, 1, options);

        Assert.NotNull(summary);
        Assert.Equal(102, summary!.Poc);
        Assert.Equal(103, summary.Vah);
        Assert.Equal(101, summary.Val);
        Assert.Equal(140, summary.Total);
        Assert.Equal(28, summary.DeltaTotal);
        Assert.True(summary.PriceInLowVolume);
        Assert.NotNull(summary.NearestLvn);
        Assert.Equal(100, summary.NearestLvn!.Price);
        Assert.Equal(102, summary.Hvn[0].Price);
        Assert.Equal(100, summary.Lvn[0].Price);
    }

    [Fact]
    public void BuildRecentMove_DetectsStrongUpMove()
    {
        var candles = Enumerable.Range(0, 20)
            .Select(i => Bar(Day.AddMinutes(i * 2), 100 + i, 101.5 + i,
                99.5 + i, 101 + i))
            .ToList();
        var options = JevFeatureOptions.Default with
        {
            MoveLookback = 20,
            StrongAtr = 2.0,
            StrongMinTicks = 20,
        };

        var move = JevContextAnalyzer.BuildRecentMove(candles, 1, options);

        Assert.Equal("alta", move.Direction);
        Assert.Equal(20, move.Candles);
        Assert.Equal(20, move.ChangeTicks);
        Assert.True(move.Strong);
        Assert.True(move.Efficiency > 0.9);
    }

    [Fact]
    public void BuildZones_SeparatesFilteredAndClusterBubbles()
    {
        var candles = new List<BarStorageItem>
        {
            Bar(Day, 99.5, 100.5, 98.8, 99.2),
        };
        var pivIntra = new PivotStorageItem
        {
            Symbol = "WINFUT",
            Date = Day,
            Source = "D-1",
            Levels = new()
            {
                new() { Key = "S2", Value = 99.0 },
                new() { Key = "S1", Value = 102.0 },
            },
        };
        var extIntra = new ExtremeStorageItem
        {
            Symbol = "WINFUT",
            Date = Day,
            Extremes = new()
            {
                new()
                {
                    Type = ExtremeType.Valley, Position = 98.9, Value = 1000,
                    Strength = 1.0, Confidence = 0.9, StartPosition = 98,
                    EndPosition = 100,
                },
            },
        };
        var filtered = new List<BubbleStorageItem>
        {
            new()
            {
                Price = 99.0, Agent = 122, Amount = 500, Date = Day.AddMinutes(1),
                ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT",
            },
        };
        var cluster = new List<BubbleStorageItem>
        {
            filtered[0],
            new()
            {
                Price = 99.1, Agent = 8, Amount = 100, Date = Day.AddMinutes(1),
                ActionType = Ticks2.ActionType.Sale, Symbol = "WINFUT",
            },
        };
        var options = JevFeatureOptions.Default with { ZoneMaxItems = 8 };

        var zones = JevContextAnalyzer.BuildZones(
            candles, pivIntra, null, extIntra, null, null, null,
            filtered, cluster, Day.AddMinutes(2), 1, options);

        var zone = Assert.Single(zones, z => z.Level == "S2");
        Assert.Equal(500, zone.FilteredBubbles.BuyAmount);
        Assert.Equal(0, zone.FilteredBubbles.SellAmount);
        Assert.Equal(2, zone.ClusterBubbles.Count);
        Assert.Equal("buy", zone.Absorption);
    }

    [Fact]
    public void EvaluateConfluence_SetupTrueForDailyTopLowVolumeAndSellAbsorption()
    {
        var zones = new List<JevZoneFeature>
        {
            new("topo", "diario", "topo", 105, 20, "resistance", Day, true,
                0.20, true,
                new JevBubbleSummary(3, 0, 3, 0, 1500, -1500),
                JevBubbleSummary.Empty, "sell", 104, 106, 2.0, 0.74, true),
            new("vale", "intra", "vale", 95, 40, "support", Day, false,
                0.25, true,
                JevBubbleSummary.Empty, JevBubbleSummary.Empty, "none",
                94, 96, 1.0, 0.60, true),
        };
        var move = new JevRecentMove("alta", 20, 80, 100, 10, 0.8, true);
        var options = JevFeatureOptions.Default;

        var confluence = JevContextAnalyzer.EvaluateConfluence(zones, move, options);

        Assert.True(confluence.AtDailyZone);
        Assert.Equal("topo", confluence.DailyZoneType);
        Assert.True(confluence.OppositeDailyZone);
        Assert.True(confluence.LowVolumeNearValley);
        Assert.True(confluence.FilteredBubblesAtZone);
        Assert.Equal("sell", confluence.AbsorptionSide);
        Assert.Equal("alta_forte", confluence.PriorMove);
        Assert.True(confluence.SetupReversaoPerdaForca);
    }

    [Fact]
    public void EvaluateConfluence_WeakMoveDoesNotSetup()
    {
        var zones = new List<JevZoneFeature>
        {
            new("topo", "diario", "topo", 105, 20, "resistance", Day, true,
                0.20, true,
                new JevBubbleSummary(3, 0, 3, 0, 1500, -1500),
                JevBubbleSummary.Empty, "sell", 104, 106, 2.0, 0.74, true),
        };
        var move = new JevRecentMove("alta", 20, 5, 30, 10, 0.2, false);

        var confluence = JevContextAnalyzer.EvaluateConfluence(
            zones, move, JevFeatureOptions.Default);

        Assert.False(confluence.SetupReversaoPerdaForca);
    }

    [Fact]
    public void StateBudget_UsesConservativeCharEstimate()
    {
        Assert.Equal(5, JevStateBudget.EstimateTokens("12345"));
        Assert.False(JevStateBudget.ExceedsTarget(new string('x', JevStateBudget.TargetTokens)));
        Assert.True(JevStateBudget.ExceedsTarget(new string('x', JevStateBudget.TargetTokens + 1)));
        Assert.True(JevStateBudget.ExceedsPerQuestionLimit(
            new string('x', JevStateBudget.PerQuestionLimitTokens + 1)));
        Assert.True(JevStateBudget.EstimateTokens(new { a = 1 }) > 0);
    }
}
