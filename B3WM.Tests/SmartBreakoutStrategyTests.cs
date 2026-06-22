using B3WM.Services;
using B3WM.Services.Backtest;
using B3WM.Services.Core;
using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using Microsoft.Extensions.Logging;
using Moq;

namespace B3WM.Tests;

public class SmartBreakoutStrategyTests
{
    private static readonly DateTime BaseDate = new(2024, 1, 2);

    private static BacktestConfig DefaultConfig => new()
    {
        Symbol = "WINFUT",
        TimeFrame = 5,
        StartDate = BaseDate,
        EndDate = BaseDate,
        StopLossPoints = 200,
        TakeProfitPoints = 400,
        Quantity = 1,
        SlippagePoints = 0,
        CommissionPerSide = 0,
        StrategyName = StrategyType.SmartBreakout,
        LookbackPeriod = 20
    };

    private sealed class FakeDataKeeper : DataKeeperBase
    {
        private readonly Dictionary<string, object> _data = new();
        public void AddData<T>(string path, T data) => _data[path] = data!;
        public override async Task<T> ReadDataAsync<T>(string path)
        {
            if (_data.TryGetValue(path, out var val))
                return await Task.FromResult((T)val);
            return new T();
        }
    }

    private static string BubblePath(string symbol, DateTime date)
        => $"{symbol}_{nameof(BubbleService)}_{date:yyyy-MM-dd}.json";

    private static SmartBreakoutStrategy CreateStrategy(DataKeeperBase keeper, BacktestConfig config)
    {
        var logger = new Mock<ILogger<SmartBreakoutStrategy>>().Object;
        return new SmartBreakoutStrategy(keeper, config, logger);
    }

    private static BarStorageItem MakeBar(double open, double high, double low, double close,
        List<VolumeLevel>? volumeLevel = null, int minute = 0)
    {
        return new BarStorageItem
        {
            Date = BaseDate.AddMinutes(minute).GetCandleStart(5),
            Symbol = "WINFUT",
            TimeFrame = 5,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = 100,
            VolumeLevel = volumeLevel
        };
    }

    private static VolumeLevel MakeVL(double price, long total)
        => new() { Price = price, Total = total, BuyVolume = total / 2, SellVolume = total / 2 };

    [Fact]
    public async Task Initialize_LoadsBubbles()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BubblePath("WINFUT", BaseDate), new List<BubbleStorageItem>
        {
            new() { Date = BaseDate.AddMinutes(1), Price = 70000, Amount = 600, ActionType = Ticks2.ActionType.Buy, Agent = 1 }
        });
        var strategy = CreateStrategy(keeper, DefaultConfig);

        await strategy.InitializeAsync();

        var bar = MakeBar(70000, 70100, 69900, 70050);
        var signal = strategy.Evaluate(bar, hasPosition: false);
        Assert.Null(signal);
    }

    [Fact]
    public async Task Evaluate_FirstBar_NoSignal_StructureInitialized()
    {
        var keeper = new FakeDataKeeper();
        var strategy = CreateStrategy(keeper, DefaultConfig);
        await strategy.InitializeAsync();

        var bar = MakeBar(70000, 70100, 69900, 70050);
        var signal = strategy.Evaluate(bar, hasPosition: false);

        Assert.Null(signal);
    }

    [Fact]
    public void Evaluate_NoBubblesForBar_ReturnsNull()
    {
        var keeper = new FakeDataKeeper();
        var strategy = CreateStrategy(keeper, DefaultConfig);

        var bar = MakeBar(70000, 70100, 69900, 70050);
        strategy.Evaluate(bar, false);
        var signal = strategy.Evaluate(bar, false);

        Assert.Null(signal);
    }

    [Fact]
    public async Task Evaluate_BuyEntry_WhenConditionsMet()
    {
        var keeper = new FakeDataKeeper();
        var barDate = BaseDate.AddMinutes(0).GetCandleStart(5);
        keeper.AddData(BubblePath("WINFUT", BaseDate), new List<BubbleStorageItem>
        {
            new() { Date = barDate.AddMinutes(1), Price = 70050, Amount = 600, ActionType = Ticks2.ActionType.Buy, Agent = 1 }
        });
        var strategy = CreateStrategy(keeper, DefaultConfig);
        await strategy.InitializeAsync();

        var bar1 = MakeBar(70000, 70200, 69800, 70100, minute: 0);
        strategy.Evaluate(bar1, false);

        var bar2 = MakeBar(70100, 70350, 70050, 70200,
            volumeLevel: new List<VolumeLevel> { MakeVL(70050, 10), MakeVL(70100, 1000), MakeVL(70200, 1000) },
            minute: 2);
        var signal = strategy.Evaluate(bar2, false);

        Assert.NotNull(signal);
        Assert.Equal(OrderSide.Buy, signal.Side);
        Assert.NotNull(signal.StopLossPrice);
        Assert.Null(signal.TakeProfitPrice);
        Assert.Contains("SmartB.Compra", signal.Reason);
    }

    [Fact]
    public async Task Evaluate_SellEntry_WhenConditionsMet()
    {
        var keeper = new FakeDataKeeper();
        var barDate = BaseDate.AddMinutes(0).GetCandleStart(5);
        keeper.AddData(BubblePath("WINFUT", BaseDate), new List<BubbleStorageItem>
        {
            new() { Date = barDate.AddMinutes(1), Price = 69950, Amount = 600, ActionType = Ticks2.ActionType.Sale, Agent = 1 }
        });
        var strategy = CreateStrategy(keeper, DefaultConfig);
        await strategy.InitializeAsync();

        var bar1 = MakeBar(70000, 70200, 69800, 70100, minute: 0);
        strategy.Evaluate(bar1, false);

        var bar2 = MakeBar(69900, 70100, 69700, 69800,
            volumeLevel: new List<VolumeLevel> { MakeVL(69950, 10), MakeVL(69800, 1000), MakeVL(70000, 1000) },
            minute: 2);
        var signal = strategy.Evaluate(bar2, false);

        Assert.NotNull(signal);
        Assert.Equal(OrderSide.Sell, signal.Side);
        Assert.NotNull(signal.StopLossPrice);
        Assert.Contains("SmartB.Venda", signal.Reason);
    }

    [Fact]
    public async Task Evaluate_Exit_WhenLargeBubbleExists()
    {
        var keeper = new FakeDataKeeper();
        var barDate = BaseDate.AddMinutes(0).GetCandleStart(5);
        keeper.AddData(BubblePath("WINFUT", BaseDate), new List<BubbleStorageItem>
        {
            new() { Date = barDate.AddMinutes(1), Price = 70000, Amount = 1500, ActionType = Ticks2.ActionType.Buy, Agent = 1 }
        });
        var strategy = CreateStrategy(keeper, DefaultConfig);
        await strategy.InitializeAsync();

        var bar1 = MakeBar(70000, 70200, 69800, 70100, minute: 0);
        strategy.Evaluate(bar1, false);

        var bar2 = MakeBar(70100, 70350, 70050, 70200, minute: 2);
        var signal = strategy.Evaluate(bar2, hasPosition: true);

        Assert.NotNull(signal);
        Assert.Equal("Exit: large bubble", signal.Reason);
    }

    [Fact]
    public async Task Evaluate_NoExit_WhenSmallBubble()
    {
        var keeper = new FakeDataKeeper();
        var barDate = BaseDate.AddMinutes(0).GetCandleStart(5);
        keeper.AddData(BubblePath("WINFUT", BaseDate), new List<BubbleStorageItem>
        {
            new() { Date = barDate.AddMinutes(1), Price = 70000, Amount = 200, ActionType = Ticks2.ActionType.Buy, Agent = 1 }
        });
        var strategy = CreateStrategy(keeper, DefaultConfig);
        await strategy.InitializeAsync();

        var bar1 = MakeBar(70000, 70200, 69800, 70100, minute: 0);
        strategy.Evaluate(bar1, false);

        var bar2 = MakeBar(70100, 70350, 70050, 70200, minute: 2);
        var signal = strategy.Evaluate(bar2, hasPosition: true);

        Assert.Null(signal);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var keeper = new FakeDataKeeper();
        var strategy = CreateStrategy(keeper, DefaultConfig);

        var bar = MakeBar(70000, 70100, 69900, 70050);
        strategy.Evaluate(bar, false);
        strategy.Reset();

        var bar2 = MakeBar(70100, 70200, 70000, 70150);
        var signal = strategy.Evaluate(bar2, false);
        Assert.Null(signal);
    }
}
