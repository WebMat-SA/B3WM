using B3WM.Services;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;
using B3WM.Shared.Models.Strategies;

namespace B3WM.Tests;

public class DataPersistencePolicyTests
{
    private static readonly DateTime Today = new(2026, 10, 19, 12, 0, 0);

    [Fact]
    public void IsToday_ComparesOnlyDate()
    {
        Assert.True(DataPersistencePolicy.IsToday(new DateTime(2026, 10, 19, 23, 59, 0), Today));
        Assert.False(DataPersistencePolicy.IsToday(new DateTime(2026, 10, 18, 23, 59, 0), Today));
    }

    [Fact]
    public void InMemoryFallback_OnlyForToday()
    {
        Assert.True(DataPersistencePolicy.ShouldUseInMemoryToday(new DateTime(2026, 10, 19, 8, 0, 0), Today));
        Assert.False(DataPersistencePolicy.ShouldUseInMemoryToday(new DateTime(2026, 10, 18, 8, 0, 0), Today));
    }

    [Fact]
    public void Volume_RequiresTodayAndNonEmpty()
    {
        Assert.False(DataPersistencePolicy.ShouldPersistVolume(null, Today));
        Assert.False(DataPersistencePolicy.ShouldPersistVolume(new VolumeLevelStorageItem
        {
            Date = Today,
            Volumes = new List<VolumeLevel>()
        }, Today));
        Assert.False(DataPersistencePolicy.ShouldPersistVolume(new VolumeLevelStorageItem
        {
            Date = Today.AddDays(-1),
            Volumes = new List<VolumeLevel> { new() { Price = 100, Total = 1 } }
        }, Today));
        Assert.True(DataPersistencePolicy.ShouldPersistVolume(new VolumeLevelStorageItem
        {
            Date = Today,
            Volumes = new List<VolumeLevel> { new() { Price = 100, Total = 1 } }
        }, Today));
    }

    [Fact]
    public void Forecast_RequiresTodayAndVolume()
    {
        Assert.False(DataPersistencePolicy.ShouldPersistForecast(null, Today));
        Assert.False(DataPersistencePolicy.ShouldPersistForecast(new AdjustmentForecastItem
        {
            Day = Today,
            SumV = 0
        }, Today));
        Assert.False(DataPersistencePolicy.ShouldPersistForecast(new AdjustmentForecastItem
        {
            Day = Today.AddDays(-1),
            Time = Today.AddDays(-1),
            SumV = 100
        }, Today));
        Assert.True(DataPersistencePolicy.ShouldPersistForecast(new AdjustmentForecastItem
        {
            Day = Today,
            Time = Today.AddHours(9),
            SumV = 100
        }, Today));
        Assert.True(DataPersistencePolicy.ShouldPersistForecast(new AdjustmentForecastItem
        {
            Day = default,
            Time = Today.AddHours(9),
            SumV = 100
        }, Today));
    }

    [Fact]
    public void Extremes_RequireFreshTodayProfileAndPoints()
    {
        Assert.False(DataPersistencePolicy.ShouldPersistExtremes(null, Today, true));
        Assert.False(DataPersistencePolicy.ShouldPersistExtremes(new ExtremeStorageItem
        {
            Date = Today,
            Statistics = new ExtremeStatistics { PointCount = 10 }
        }, Today, false));
        Assert.False(DataPersistencePolicy.ShouldPersistExtremes(new ExtremeStorageItem
        {
            Date = Today.AddDays(-1),
            Statistics = new ExtremeStatistics { PointCount = 10 }
        }, Today, true));
        Assert.False(DataPersistencePolicy.ShouldPersistExtremes(new ExtremeStorageItem
        {
            Date = Today,
            Statistics = new ExtremeStatistics { PointCount = 0 }
        }, Today, true));
        Assert.True(DataPersistencePolicy.ShouldPersistExtremes(new ExtremeStorageItem
        {
            Date = Today,
            Statistics = new ExtremeStatistics { PointCount = 10 }
        }, Today, true));
    }

    [Fact]
    public void Structure_RequiresTodayBar()
    {
        Assert.False(DataPersistencePolicy.ShouldPersistStructure(null, Today));
        Assert.False(DataPersistencePolicy.ShouldPersistStructure(new BarStorageItem
        {
            Date = Today.AddDays(-1)
        }, Today));
        Assert.True(DataPersistencePolicy.ShouldPersistStructure(new BarStorageItem
        {
            Date = Today
        }, Today));
    }

    [Fact]
    public void StrategyLog_RequiresDecisionOrTrade()
    {
        Assert.False(DataPersistencePolicy.ShouldPersistStrategyLog(null));
        Assert.False(DataPersistencePolicy.ShouldPersistStrategyLog(new StrategyLogDay()));
        Assert.True(DataPersistencePolicy.ShouldPersistStrategyLog(new StrategyLogDay
        {
            Decisions = new List<StrategyDecisionLog> { new() }
        }));
        Assert.True(DataPersistencePolicy.ShouldPersistStrategyLog(new StrategyLogDay
        {
            PaperTrades = new List<PaperTrade> { new() }
        }));
    }
}
