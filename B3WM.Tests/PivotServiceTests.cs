using B3WM.Services.Core;
using B3WM.Shared.Models;

namespace B3WM.Tests;

/// <summary>
/// Pivot Tradicional do Profit (issue #14): P=(H+L+C)/3, 2–5 linhas.
/// </summary>
public class PivotServiceTests
{
    [Fact]
    public void ComputeTraditional_TwoLines_KnownValues()
    {
        // H=190000 L=188000 C=189000 → P=189000
        var levels = PivotService.ComputeTraditional(190000, 188000, 189000, 2)
            .ToDictionary(l => l.Key, l => l.Value);

        Assert.Equal(5, levels.Count);
        Assert.Equal(189000, levels["P"], precision: 6);
        Assert.Equal(190000, levels["R1"], precision: 6); // 2P-L
        Assert.Equal(188000, levels["S1"], precision: 6); // 2P-H
        Assert.Equal(191000, levels["R2"], precision: 6); // P+range
        Assert.Equal(187000, levels["S2"], precision: 6); // P-range
    }

    [Fact]
    public void ComputeTraditional_LineCountControlsPairs()
    {
        Assert.Equal(5, PivotService.ComputeTraditional(100, 90, 95, 2).Count);
        Assert.Equal(7, PivotService.ComputeTraditional(100, 90, 95, 3).Count);
        Assert.Equal(9, PivotService.ComputeTraditional(100, 90, 95, 4).Count);
        Assert.Equal(11, PivotService.ComputeTraditional(100, 90, 95, 5).Count);
    }

    [Fact]
    public void ComputeTraditional_ClampsLineCount()
    {
        Assert.Equal(5, PivotService.ComputeTraditional(100, 90, 95, 0).Count);
        Assert.Equal(5, PivotService.ComputeTraditional(100, 90, 95, 1).Count);
        Assert.Equal(11, PivotService.ComputeTraditional(100, 90, 95, 99).Count);
    }

    [Fact]
    public void ComputeTraditional_ThreeLines_R3S3()
    {
        var levels = PivotService.ComputeTraditional(190000, 188000, 189000, 3)
            .ToDictionary(l => l.Key, l => l.Value);
        // R3 = H + 2(P-L) = 192000; S3 = L - 2(H-P) = 186000
        Assert.Equal(192000, levels["R3"], precision: 6);
        Assert.Equal(186000, levels["S3"], precision: 6);
    }

    [Fact]
    public void ComputeTraditional_InvalidHlc_Throws()
    {
        Assert.Throws<ArgumentException>(() => PivotService.ComputeTraditional(90, 100, 95, 2));
        Assert.Throws<ArgumentException>(() => PivotService.ComputeTraditional(double.NaN, 90, 95, 2));
    }

    private static BarStorageItem Day(string date, double h, double l, double c) =>
        new() { Date = DateTime.Parse(date), Symbol = "WINFUT", TimeFrame = 1440, High = h, Low = l, Close = c };

    [Fact]
    public void ResolveIntradaySource_PicksDayBeforeSession_SkipsGap()
    {
        var bars = new List<BarStorageItem>
        {
            Day("2026-09-10T00:00:00", 100, 90, 95), // qua
            Day("2026-09-11T00:00:00", 102, 91, 96), // qui (D-1)
            // sex 12 sem pregão → sessão exibida continua qui 11? não: ResolveSessionDate dá 11
        };
        var src = PivotService.ResolveIntradaySource(bars, DateTime.Parse("2026-09-11T00:00:00"));
        Assert.NotNull(src);
        Assert.Equal(DateTime.Parse("2026-09-10T00:00:00"), src!.Date);
    }

    [Fact]
    public void ResolveSessionDate_TodayWithoutSession_FallsBackToLast()
    {
        var bars = new List<BarStorageItem>
        {
            Day("2026-09-10T00:00:00", 100, 90, 95),
            Day("2026-09-11T00:00:00", 102, 91, 96),
        };
        // Sábado 12 sem barra → sessão = qui 11.
        var session = PivotService.ResolveSessionDate(bars, DateTime.Parse("2026-09-12T00:00:00"));
        Assert.Equal(DateTime.Parse("2026-09-11T00:00:00"), session);
    }

    [Fact]
    public void ResolveDailySource_AggregatesPreviousWeek()
    {
        // Semana 1 (1–5/set): H máx 110, L mín 80, close último dia 105.
        var bars = new List<BarStorageItem>
        {
            Day("2026-09-01T00:00:00", 100, 90, 95),
            Day("2026-09-02T00:00:00", 105, 85, 100),
            Day("2026-09-03T00:00:00", 110, 95, 108),
            Day("2026-09-04T00:00:00", 108, 80, 90),
            Day("2026-09-05T00:00:00", 104, 88, 105),
            Day("2026-09-08T00:00:00", 112, 100, 110), // seg semana atual
        };
        var src = PivotService.ResolveDailySource(bars, DateTime.Parse("2026-09-09T00:00:00"));
        Assert.NotNull(src);
        Assert.Equal(110, src!.Value.High, precision: 6);
        Assert.Equal(80, src!.Value.Low, precision: 6);
        Assert.Equal(105, src!.Value.Close, precision: 6);
    }

    [Fact]
    public void ResolveDailySource_WithoutPreviousWeek_ReturnsNull()
    {
        var bars = new List<BarStorageItem>
        {
            Day("2026-09-08T00:00:00", 112, 100, 110),
        };
        Assert.Null(PivotService.ResolveDailySource(bars, DateTime.Parse("2026-09-08T00:00:00")));
    }
}
