using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Market;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace B3WM.Tests;

public class ScreenStateBuilderAsOfTests
{
    private static readonly DateTime Day = new(2026, 10, 19);
    private static readonly DateTime AsOf = Day.AddHours(18).AddMinutes(11).AddSeconds(36);

    private static BarStorageItem Bar(int hour, int minute, int tf,
        double open, double high, double low, double close, long volume = 10) => new()
    {
        Date = Day.AddHours(hour).AddMinutes(minute),
        Symbol = "WINFUT",
        TimeFrame = tf,
        Open = open,
        High = high,
        Low = low,
        Close = close,
        Volume = volume,
        VwapPrice = close,
    };

    private static StructureStorageItem Structure(int hour, int minute, int tf,
        double up, double down) => new()
    {
        Date = tf == 1440
            ? Day.AddHours(hour).AddMinutes(minute)
            : Day.AddHours(hour).AddMinutes(minute),
        Symbol = "WINFUT",
        TimeFrame = tf,
        UpBorder = up,
        DownBorder = down,
        UpAuxBorder = 0,
        DownAuxBorder = 0,
    };

    [Fact]
    public async Task BuildAsync_ReplayAtBubble_CutsCandlesStructuresBubblesAndDaily()
    {
        var bars2 = new List<BarStorageItem>
        {
            Bar(18, 0, 2, 100, 100.5, 99.5, 100.2),
            Bar(18, 2, 2, 100.2, 101, 100, 100.8),
            Bar(18, 4, 2, 100.8, 101.2, 100.5, 101),
            Bar(18, 6, 2, 101, 101.3, 100.7, 101.1),
            Bar(18, 8, 2, 101.1, 101.4, 100.9, 101.2),
            Bar(18, 10, 2, 101.2, 102, 101, 101.9),
            Bar(18, 12, 2, 101.9, 102.2, 101.5, 102),
        };
        var bars1 = new List<BarStorageItem>();
        for (var i = 0; i <= 11; i++)
            bars1.Add(Bar(18, i, 1, 200 + i, 200.5 + i, 199.5 + i, 200 + i));

        var intradayStructs = new List<StructureStorageItem>
        {
            Structure(10, 0, 2, 98, 90),
            Structure(18, 0, 2, 100, 92),
            Structure(18, 2, 2, 100.2, 92),
            Structure(18, 8, 2, 100.5, 93),
            Structure(18, 10, 2, 101, 94),
            Structure(18, 12, 2, 101.5, 95),
        };
        var dailyStructs = new List<StructureStorageItem>
        {
            new() { Date = Day.AddDays(-1), Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 110, DownBorder = 90 },
            new() { Date = Day, Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 120, DownBorder = 80 },
        };
        var dailyBars = new List<BarStorageItem>
        {
            new() { Date = Day.AddDays(-3), Symbol = "WINFUT", TimeFrame = 1440, Open = 190, High = 195, Low = 185, Close = 200, Volume = 1000 },
            new() { Date = Day.AddDays(-2), Symbol = "WINFUT", TimeFrame = 1440, Open = 200, High = 205, Low = 195, Close = 201, Volume = 1000 },
            new() { Date = Day.AddDays(-1), Symbol = "WINFUT", TimeFrame = 1440, Open = 201, High = 206, Low = 196, Close = 202, Volume = 1000 },
            new() { Date = Day, Symbol = "WINFUT", TimeFrame = 1440, Open = 202, High = 210, Low = 190, Close = 999, Volume = 9999 },
        };
        var bubbles = new List<BubbleStorageItem>
        {
            new() { Date = Day.AddHours(18).AddMinutes(0), Agent = 122, Amount = 100, Price = 100, ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT" },
            new() { Date = Day.AddHours(18).AddMinutes(5), Agent = 122, Amount = 500, Price = 101, ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT" },
            new() { Date = AsOf, Agent = 122, Amount = 500, Price = 101.2, ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT" },
            new() { Date = Day.AddHours(18).AddMinutes(12), Agent = 122, Amount = 500, Price = 102, ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT" },
        };
        var levels = new List<VolumeLevel>
        {
            new() { Price = 100, Total = 100, BuyVolume = 60, SellVolume = 40 },
        };

        var market = new Mock<IMarketData>();
        market.Setup(m => m.CandlesAsync("WINFUT", 2, Day, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bars2);
        market.Setup(m => m.CandlesAsync("WINFUT", 1, Day, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bars1);
        market.Setup(m => m.StructuresAsync("WINFUT", 2, It.IsAny<double>(), Day, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intradayStructs);
        market.Setup(m => m.DailyStructuresAsync("WINFUT", It.IsAny<double>(), Day, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dailyStructs);
        market.Setup(m => m.DailyCandlesAsync("WINFUT", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dailyBars);
        market.Setup(m => m.BubblesAsync("WINFUT", Day, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bubbles);
        market.Setup(m => m.VolumeLevelsAsync("WINFUT", Day, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(levels);
        market.Setup(m => m.IntradayPivotAsync("WINFUT", It.IsAny<int>(), Day, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PivotStorageItem?)null);
        market.Setup(m => m.DailyPivotAsync("WINFUT", It.IsAny<int>(), Day, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PivotStorageItem?)null);
        market.Setup(m => m.DailyExtremesAsync("WINFUT", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<double>(), It.IsAny<double>(), AsOf, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtremeStorageItem?)null);
        market.Setup(m => m.VwapAsync("WINFUT", Day, It.IsAny<CancellationToken>()))
            .ReturnsAsync((double?)null);

        using var doc = JsonDocument.Parse("""
        {
          "profileAutoByPriceStructure": true,
          "profileVisible": true,
          "bubbleVisible": true,
          "bubbleAgentsFilter": false,
          "bubbleAmountFilter": true,
          "thresholdBubble": 475,
          "structureVisible": true,
          "structureAuxVisible": true,
          "extremeVisible": true,
          "pivotVisible": true,
          "vwapVisible": true
        }
        """);
        var builder = new ScreenStateBuilder(market.Object, NullLogger<ScreenStateBuilder>.Instance);

        var snap = await builder.BuildAsync(new ScreenStateRequest
        {
            Symbol = "WINFUT",
            TimeFrame = 2,
            DisplayDate = Day,
            AsOf = AsOf,
            VisibleTimeFrames = new() { 2 },
            Spec = doc.RootElement.Clone(),
            ResolveScope = true,
            IncludeDaily = true,
        });

        var candles = snap.Get<List<BarStorageItem>>(SnapshotLayers.Candles)!;
        Assert.Equal(5, candles.Count);
        Assert.Equal(Day.AddHours(18).AddMinutes(8), candles[^1].Date);

        var scope = snap.Get<DateWindow>(SnapshotLayers.ScopeIntraday)!;
        Assert.Equal(Day.AddHours(18), scope.From);
        Assert.Equal(Day.AddHours(18).AddMinutes(8), scope.To);

        var structs = snap.Get<List<StructureStorageItem>>(SnapshotLayers.Structures)!;
        Assert.DoesNotContain(structs, s => s.TimeFrame == 2 && s.Date > scope.To);
        Assert.DoesNotContain(structs, s => s.TimeFrame == 1440 && s.Date.Date == Day.Date);

        var sentBubbles = snap.Get<List<BubbleStorageItem>>(SnapshotLayers.Bubbles)!;
        Assert.Equal(2, sentBubbles.Count);
        Assert.Equal(AsOf, sentBubbles[^1].Date);

        var daily = snap.Get<List<BarStorageItem>>(SnapshotLayers.DailyCandles)!;
        var partial = Assert.Single(daily.Where(b => b.Date.Date == Day.Date));
        Assert.Equal(200, partial.Open);
        Assert.Equal(210, partial.Close);
        Assert.Equal(110, partial.Volume);
        Assert.NotEqual(999, partial.Close);

        market.Verify(m => m.VolumeLevelsAsync("WINFUT", Day, It.IsAny<DateTime?>(), AsOf,
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        var state = JevPrompt.BuildStateJson("WINFUT", 2, snap);
        Assert.Equal(5, state.Intraday.Candles.Count);
        Assert.Equal(scope.To, state.Intraday.Scope.To);
        Assert.Equal(210, Assert.Single(state.Daily.Candles.Where(c => c.Date.Date == Day.Date)).Close);
        Assert.All(state.Intraday.Structures, s => Assert.True(s.Date <= scope.To));
    }
}
