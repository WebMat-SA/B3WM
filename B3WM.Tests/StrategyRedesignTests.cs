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

public class StrategyRedesignTests
{
    private static readonly DateTime BaseDate = new(2024, 1, 2);

    private static BacktestConfig Config => new()
    {
        Symbol = "WINFUT",
        TimeFrame = 5,
        StartDate = BaseDate,
        EndDate = BaseDate,
        Quantity = 1,
        CommissionPerSide = 0,
        StrategyName = StrategyType.SmartBreakout,
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

    private static BarStorageItem MakeBar(double open, double high, double low, double close,
        List<VolumeLevel>? vl = null, int minute = 0) => new()
        {
            Date = BaseDate.AddMinutes(minute).GetCandleStart(5),
            Symbol = "WINFUT",
            TimeFrame = 5,
            Open = open, High = high, Low = low, Close = close,
            Volume = 100, VolumeLevel = vl
        };

    private static VolumeLevel VL(double price, long total)
        => new() { Price = price, Total = total, BuyVolume = total / 2, SellVolume = total / 2 };

    [Fact]
    public async Task Reset_PreservesLoadedBubbles_AllowsRerun()
    {
        var keeper = new FakeDataKeeper();
        var barDate = BaseDate.AddMinutes(0).GetCandleStart(5);
        keeper.AddData($"WINFUT_{nameof(BubbleService)}_{BaseDate:yyyy-MM-dd}.json",
            new List<BubbleStorageItem>
            {
                new() { Date = barDate.AddMinutes(1), Price = 70050, Amount = 600, ActionType = Ticks2.ActionType.Buy, Agent = 1 }
            });
        var strategy = new SmartBreakoutStrategy(keeper, Config, Mock.Of<ILogger<SmartBreakoutStrategy>>());
        await strategy.InitializeAsync();

        var bar1 = MakeBar(70000, 70200, 69800, 70100, minute: 0);
        strategy.TryGetEntry(bar1);
        var bar2 = MakeBar(70100, 70350, 70050, 70200,
            new List<VolumeLevel> { VL(70050, 10), VL(70100, 1000), VL(70200, 1000) }, minute: 2);
        var first = strategy.TryGetEntry(bar2);
        Assert.NotNull(first);

        // Reset limpa só transitório; re-run sem Initialize deve repetir o sinal.
        strategy.Reset();
        strategy.TryGetEntry(bar1);
        var second = strategy.TryGetEntry(bar2);
        Assert.NotNull(second);
        Assert.Equal(first.Reason, second.Reason);
    }

    [Fact]
    public void Factory_CreatesBothStrategies()
    {
        var keeper = new FakeDataKeeper();
        var factory = new StrategyFactory(keeper, Mock.Of<ILogger<SmartBreakoutStrategy>>());

        var simple = factory.Create(new BacktestConfig { StrategyName = StrategyType.Breakout, Symbol = "WINFUT" });
        var smart = factory.Create(new BacktestConfig { StrategyName = StrategyType.SmartBreakout, Symbol = "WINFUT" });

        Assert.IsType<SimpleBreakoutStrategy>(simple);
        Assert.IsType<SmartBreakoutStrategy>(smart);
        Assert.Throws<ArgumentException>(() => factory.Create(new BacktestConfig { StrategyName = (StrategyType)999 }));
    }

    [Fact]
    public void ExitSignal_Reason_PropagatesToEvent()
    {
        var config = new BacktestConfig { Symbol = "WINFUT", TimeFrame = 5, IsDayTrade = false };
        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.TryGetEntry(It.IsAny<BarStorageItem>()))
            .Returns(new EntrySignal { Side = OrderSide.Buy, Reason = "E", StopLossPrice = 69700, TakeProfitPrice = 70500 })
            .Returns((EntrySignal?)null);
        strategy.SetupSequence(s => s.TryGetExit(It.IsAny<BarStorageItem>(), It.IsAny<BacktestPosition>()))
            .Returns(new ExitSignal { Reason = "custom exit" })
            .Returns((ExitSignal?)null);

        var sim = new BacktestSimulator(config, strategy.Object);
        var bar1 = new BarStorageItem { Date = BaseDate, Symbol = "WINFUT", TimeFrame = 5, Open = 70000, High = 70100, Low = 69900, Close = 70000 };
        var bar2 = new BarStorageItem { Date = BaseDate.AddMinutes(5), Symbol = "WINFUT", TimeFrame = 5, Open = 70050, High = 70150, Low = 69950, Close = 70100 };
        sim.ProcessBar(bar1);
        var events = sim.ProcessBar(bar2);

        var exit = events.FirstOrDefault(e => !e.PositionOpen);
        Assert.NotNull(exit);
        Assert.Equal("custom exit", exit.Reason);
    }

    [Fact]
    public void Strategies_ExposeStructureLines_WithoutDowncast()
    {
        IStrategy simple = new SimpleBreakoutStrategy(Config);
        IStrategy smart = new SmartBreakoutStrategy(null, Config, null);

        Assert.IsAssignableFrom<IStructureProvider>(simple);
        Assert.IsAssignableFrom<IStructureProvider>(smart);
        Assert.IsAssignableFrom<IBubbleConsumer>(smart);
    }
}
