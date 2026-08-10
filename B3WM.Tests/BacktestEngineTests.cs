using B3WM.Services;
using B3WM.Services.Backtest;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using Microsoft.Extensions.Logging;
using Moq;

namespace B3WM.Tests;

public class BacktestEngineTests
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
        CommissionPerSide = 0,
        StrategyName = StrategyType.Breakout,
        LookbackPeriod = 1
    };

    private static BacktestConfig ConfigWith(BacktestConfig cfg, DateTime? endDate = null,
        double? sl = null, double? tp = null,
        double? commission = null, int? quantity = null)
    {
        return new BacktestConfig
        {
            Symbol = cfg.Symbol,
            TimeFrame = cfg.TimeFrame,
            StartDate = cfg.StartDate,
            EndDate = endDate ?? cfg.EndDate,
            StopLossPoints = sl ?? cfg.StopLossPoints,
            TakeProfitPoints = tp ?? cfg.TakeProfitPoints,
            Quantity = quantity ?? cfg.Quantity,
            CommissionPerSide = commission ?? cfg.CommissionPerSide,
            StrategyName = cfg.StrategyName,
            LookbackPeriod = cfg.LookbackPeriod
        };
    }

    private static BarStorageItem MakeBar(double open, double high, double low, double close, int minute = 0)
    {
        return new BarStorageItem
        {
            Date = BaseDate.AddMinutes(minute),
            Symbol = "WINFUT",
            TimeFrame = 5,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = 100
        };
    }

    private static string BarPath(string symbol, int tf, DateTime date)
        => $"{symbol}_CandleService_{tf}MIN_{date:yyyy-MM-dd}.json";

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

    private static BacktestEngine CreateEngine(DataKeeperBase keeper)
    {
        var logger = new Mock<ILogger<BacktestEngine>>().Object;
        return new BacktestEngine(keeper, logger);
    }

    [Fact]
    public async Task NoBars_ReturnsEmptyResult()
    {
        var keeper = new FakeDataKeeper();
        var engine = CreateEngine(keeper);
        var config = DefaultConfig;
        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), It.IsAny<bool>())).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Equal(0, result.TotalTrades);
        Assert.Empty(result.Trades);
        Assert.Empty(result.EquityCurve);
    }

    [Fact]
    public async Task NoSignals_ReturnsEmptyResult()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000)
        });
        var engine = CreateEngine(keeper);
        var config = DefaultConfig;
        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), It.IsAny<bool>())).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Equal(0, result.TotalTrades);
    }

    [Fact]
    public async Task BuyTrade_TakeProfitHit()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70100, 70500, 70100, 70450, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.TakeProfit, trade.ExitReason);
        Assert.Equal(OrderSide.Buy, trade.Side);
        Assert.Equal(70100, trade.EntryPrice);
        Assert.Equal(70500, trade.ExitPrice);
        Assert.Equal(400, trade.Points);
    }

    [Fact]
    public async Task BuyTrade_StopLossHit()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(69900, 70000, 69700, 69800, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.StopLoss, trade.ExitReason);
        Assert.Equal(69900, trade.EntryPrice);
        Assert.Equal(69700, trade.ExitPrice);
        Assert.Equal(-200, trade.Points);
    }

    [Fact]
    public async Task BuyTrade_BothHit_SLPessimistic()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70100, 70500, 69700, 70200, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69900, TakeProfitPrice = 70500 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.StopLoss, trade.ExitReason);
        Assert.Equal(69900, trade.ExitPrice);
        Assert.Equal(-200, trade.Points);
    }

    [Fact]
    public async Task SellTrade_TakeProfitHit()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70000, 70100, 69500, 69800, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Sell, Reason = "Entry", StopLossPrice = 70200, TakeProfitPrice = 69600 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.TakeProfit, trade.ExitReason);
        Assert.Equal(OrderSide.Sell, trade.Side);
        Assert.Equal(70000, trade.EntryPrice);
        Assert.Equal(69600, trade.ExitPrice);
        Assert.Equal(400, trade.Points);
    }

    [Fact]
    public async Task SellTrade_StopLossHit()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70000, 70300, 69900, 70200, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Sell, Reason = "Entry", StopLossPrice = 70200, TakeProfitPrice = 69600 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.StopLoss, trade.ExitReason);
        Assert.Equal(70200, trade.ExitPrice);
        Assert.Equal(-200, trade.Points);
    }

    [Fact]
    public async Task SellTrade_BothHit_SLPessimistic()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70000, 70300, 69500, 69900, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Sell, Reason = "Entry", StopLossPrice = 70200, TakeProfitPrice = 69600 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.StopLoss, trade.ExitReason);
        Assert.Equal(70200, trade.ExitPrice);
        Assert.Equal(-200, trade.Points);
    }

    [Fact]
    public async Task StrategySignal_Exit()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70050, 70150, 69950, 70100, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 })
            .Returns((Signal?)null);
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), true))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Exit signal" })
            .Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.StrategySignal, trade.ExitReason);
        Assert.Equal(70050, trade.EntryPrice);
        Assert.Equal(70100, trade.ExitPrice);
        Assert.Equal(50, trade.Points);
    }

    [Fact]
    public async Task EndOfData_Exit()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70100, 70200, 70000, 70150, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5));

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 });
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        Assert.Equal(ExitReason.EndOfData, result.Trades[0].ExitReason);
        Assert.Equal(70100, result.Trades[0].EntryPrice);
        Assert.Equal(70150, result.Trades[0].ExitPrice);
        Assert.Equal(50, result.Trades[0].Points);
    }

    [Fact]
    public async Task CustomSLTP_FromSignal()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70100, 70500, 70100, 70450, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5),
            sl: 999, tp: 999);

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69900, TakeProfitPrice = 70200 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(ExitReason.TakeProfit, trade.ExitReason);
        Assert.Equal(70100, trade.EntryPrice);
        Assert.Equal(70200, trade.ExitPrice);
        Assert.Equal(100, trade.Points);
    }

    [Fact]
    public async Task Commission_Calculated()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70100, 70500, 70100, 70450, 5)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(5), commission: 5.0);

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Single(result.Trades);
        var trade = result.Trades[0];
        Assert.Equal(70100, trade.EntryPrice);
        Assert.Equal(70500, trade.ExitPrice);
        Assert.Equal(400, trade.Points);
        Assert.Equal(10.0, trade.Commission);
        Assert.Equal(400 - 10.0, trade.ProfitLoss);
    }

    [Fact]
    public async Task MultipleTrades_Metrics_Correct()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(70100, 70500, 70100, 70450, 5),
            MakeBar(70500, 70600, 70400, 70500, 10),
            MakeBar(70550, 71000, 70500, 70700, 15)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(15),
            commission: 1.0);

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        int call = 0;
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(() =>
            {
                call++;
                return call switch
                {
                    1 => new Signal { Side = OrderSide.Buy, Reason = "Entry1", StopLossPrice = 69700, TakeProfitPrice = 70500 },
                    3 => new Signal { Side = OrderSide.Buy, Reason = "Entry2", StopLossPrice = 70150, TakeProfitPrice = 70950 },
                    _ => null
                };
            });
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Equal(2, result.TotalTrades);
        Assert.Equal(2, result.WinCount);
        Assert.Equal(0, result.LossCount);
        Assert.Equal(1.0, result.WinRate);
        Assert.Equal(796.0, result.NetProfit, 1);
        Assert.True(double.IsPositiveInfinity(result.ProfitFactor));
        Assert.Equal(4.0, result.TotalCommission, 1);
    }

    [Fact]
    public async Task MaxDrawdown_Calculated()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 0),
            MakeBar(69900, 70000, 69700, 69800, 5),
            MakeBar(70000, 70100, 69900, 70000, 10),
            MakeBar(70100, 70500, 70100, 70450, 15)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(15),
            commission: 0);

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        int call = 0;
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(() =>
            {
                call++;
                return call switch
                {
                    1 => new Signal { Side = OrderSide.Buy, Reason = "Entry1", StopLossPrice = 69700, TakeProfitPrice = 70300 },
                    3 => new Signal { Side = OrderSide.Buy, Reason = "Entry2", StopLossPrice = 69900, TakeProfitPrice = 70500 },
                    _ => null
                };
            });
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Equal(2, result.TotalTrades);
        Assert.Equal(1, result.WinCount);
        Assert.Equal(1, result.LossCount);
        Assert.Equal(0.5, result.WinRate);
        Assert.Equal(200, result.NetProfit, 1);
        Assert.Equal(200, result.MaxDrawdown, 1);
    }

    [Fact]
    public async Task NoEntry_AfterDayTradeCloseTime()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 595),  // 09:55
            MakeBar(70100, 70500, 70100, 70450, 605)   // 10:05 (>= 10:00)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(605));
        config.IsDayTrade = true;
        config.DayTradeCloseTime = "10:00";

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 });
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Equal(0, result.TotalTrades);
    }

    [Fact]
    public async Task Entry_BeforeDayTradeCloseTime()
    {
        var keeper = new FakeDataKeeper();
        keeper.AddData(BarPath("WINFUT", 5, BaseDate), new List<BarStorageItem>
        {
            MakeBar(70000, 70100, 69900, 70000, 595),  // 09:55
            MakeBar(70100, 70500, 70100, 70450, 598)   // 09:58 (< 10:00)
        });
        var engine = CreateEngine(keeper);
        var config = ConfigWith(DefaultConfig, endDate: BaseDate.AddMinutes(598));
        config.IsDayTrade = true;
        config.DayTradeCloseTime = "10:00";

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 });
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var result = await engine.Run(config, strategy.Object);

        Assert.Equal(1, result.TotalTrades);
        Assert.Equal(ExitReason.TakeProfit, result.Trades[0].ExitReason);
    }

    [Fact]
    public void Simulator_Incremental_ProducesEntryAndExitEvents()
    {
        var config = DefaultConfig;
        config.IsDayTrade = false;

        var strategy = new Mock<IStrategy>();
        strategy.Setup(s => s.Name).Returns("Test");
        strategy.SetupSequence(s => s.Evaluate(It.IsAny<BarStorageItem>(), false))
            .Returns(new Signal { Side = OrderSide.Buy, Reason = "Entry", StopLossPrice = 69700, TakeProfitPrice = 70500 })
            .Returns((Signal?)null);
        strategy.Setup(s => s.Evaluate(It.IsAny<BarStorageItem>(), true)).Returns((Signal?)null);

        var sim = new BacktestSimulator(config, strategy.Object);

        var ev1 = sim.ProcessBar(MakeBar(70000, 70100, 69900, 70000, 0));
        Assert.Empty(ev1); // só sinal pendente

        var ev2 = sim.ProcessBar(MakeBar(70100, 70500, 70100, 70450, 5));
        Assert.Equal(2, ev2.Count); // entrada + saída (TP)
        Assert.Equal(SignalType.EntryBuy, ev2[0].Type);
        Assert.Equal(SignalType.ExitTakeProfit, ev2[1].Type);
        Assert.Equal(70100, ev2[0].EntryPrice);
        Assert.Equal(1, sim.Trades.Count);
        Assert.Equal(ExitReason.TakeProfit, sim.Trades[0].ExitReason);
    }
}
