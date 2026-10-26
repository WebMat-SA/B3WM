using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace B3WM.Tests;

public class VwapServiceTests
{
    private static readonly DateTime Day = new(2026, 8, 14);

    private static BarStorageItem Bar1Min(DateTime date, double open, double high, double low, double close, long volume) => new()
    {
        Date = date,
        Symbol = "WINFUT",
        TimeFrame = 1,
        Open = open,
        High = high,
        Low = low,
        Close = close,
        Volume = volume,
    };

    private sealed class TmpKeeper : DataKeeperBase
    {
        private readonly string _root;
        public TmpKeeper(string root) => _root = root;
        protected override string RootDirectory => _root;
    }

    private static (VwapService svc, TmpKeeper keeper, string tmp) CreateService()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "b3wm_vwap_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var keeper = new TmpKeeper(tmp);

        var scopedSp = new Mock<IServiceProvider>();
        scopedSp.Setup(s => s.GetService(typeof(DataKeeperBase))).Returns(keeper);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(scopedSp.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactory.Object);
        sp.Setup(s => s.GetService(typeof(DataKeeperBase))).Returns(keeper);

        var clients = new Mock<IHubClients<IDataHubClient>>();
#pragma warning disable CS0618 // ReceiveOnForecast mantido por compat
        var group = new Mock<IDataHubClient>();
        group.Setup(c => c.ReceiveVwapUpdate(It.IsAny<VwapHistoryPoint>())).Returns(Task.CompletedTask);
        group.Setup(c => c.ReceiveVwapHistory(It.IsAny<List<VwapHistoryPoint>>())).Returns(Task.CompletedTask);
        group.Setup(c => c.ReceiveOnForecast(It.IsAny<AdjustmentForecastItem>())).Returns(Task.CompletedTask);
#pragma warning restore CS0618
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        var hub = new Mock<IHubContext<DataHub, IDataHubClient>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        var svc = new VwapService("WINFUT", hub.Object, sp.Object, NullLogger<VwapService>.Instance);
        return (svc, keeper, tmp);
    }

    private static async Task<VwapService> SettledService()
    {
        var (svc, _, _) = CreateService();
        // O ctor dispara InitAsync em background (dir temporário vazio: rápido).
        await Task.Delay(500);
        return svc;
    }

    private static void Cleanup(string tmp)
    {
        try { Directory.Delete(tmp, recursive: true); } catch { }
    }

    [Fact]
    public void TypicalPrice_IsHlcDividedBy3()
    {
        var bar = Bar1Min(Day.AddHours(9), 100, 110, 90, 104, 10);
        Assert.Equal((110 + 90 + 104) / 3.0, VwapService.TypicalPrice(bar), precision: 10);
    }

    [Fact]
    public async Task Append_TwoBars_AccumulatesTypicalVwap()
    {
        var (svc, keeper, tmp) = CreateService();
        try
        {
            await Task.Delay(500);
            var b1 = Bar1Min(Day.AddHours(9), 100, 110, 90, 104, 10); // typical 101.333...
            var b2 = Bar1Min(Day.AddHours(9).AddMinutes(1), 104, 108, 102, 106, 20); // typical 105.333...

            await svc.AppendClosedBarAsync(b1);
            var p2 = await svc.AppendClosedBarAsync(b2);

            double t1 = (110 + 90 + 104) / 3.0, t2 = (108 + 102 + 106) / 3.0;
            double expected = (t1 * 10 + t2 * 20) / 30;
            Assert.NotNull(p2);
            Assert.Equal(expected, p2!.Vwap, precision: 6);
            Assert.Equal(30, p2.SumV);

            var history = svc.GetHistory();
            Assert.Equal(2, history.Count);

            // Persistiu 1 ponto por minuto no arquivo do dia.
            var file = await keeper.ReadDataAsync<List<VwapHistoryPoint>>($"WINFUT_{nameof(VwapService)}_{Day:yyyy-MM-dd}.json");
            Assert.Equal(2, file.Count);
        }
        finally { Cleanup(tmp); }
    }

    [Fact]
    public async Task Append_GapMinutes_FillsWithLastVwap()
    {
        var (svc, _, tmp) = CreateService();
        try
        {
            await Task.Delay(500);
            await svc.AppendClosedBarAsync(Bar1Min(Day.AddHours(9), 100, 100, 100, 100, 10));
            // Pula 09:01 e 09:02.
            await svc.AppendClosedBarAsync(Bar1Min(Day.AddHours(9).AddMinutes(3), 110, 110, 110, 110, 10));

            var history = svc.GetHistory();
            Assert.Equal(4, history.Count);
            // Fillers repetem VWAP e somas do último ponto real.
            Assert.Equal(history[0].Vwap, history[1].Vwap, precision: 10);
            Assert.Equal(history[0].Vwap, history[2].Vwap, precision: 10);
            Assert.Equal(history[0].SumV, history[1].SumV);
            Assert.Equal(Day.AddHours(9).AddMinutes(1), history[1].Minute);
            // Último ponto incorpora a barra nova.
            Assert.Equal((100.0 * 10 + 110.0 * 10) / 20, history[3].Vwap, precision: 6);
        }
        finally { Cleanup(tmp); }
    }

    [Fact]
    public async Task Append_SameBarTwice_DoesNotDoubleCount()
    {
        var (svc, _, tmp) = CreateService();
        try
        {
            await Task.Delay(500);
            var bar = Bar1Min(Day.AddHours(9), 100, 100, 100, 100, 10);
            var p1 = await svc.AppendClosedBarAsync(bar);
            var p2 = await svc.AppendClosedBarAsync(bar);
            Assert.Equal(p1!.Vwap, p2!.Vwap, precision: 10);
            Assert.Equal(p1.SumV, p2!.SumV);
            Assert.Single(svc.GetHistory());
        }
        finally { Cleanup(tmp); }
    }

    [Fact]
    public async Task Append_Non1MinBar_Ignored()
    {
        var (svc, _, tmp) = CreateService();
        try
        {
            await Task.Delay(500);
            var bar = Bar1Min(Day.AddHours(9), 100, 100, 100, 100, 10);
            bar.TimeFrame = 5;
            Assert.Null(await svc.AppendClosedBarAsync(bar));
            Assert.Empty(svc.GetHistory());
        }
        finally { Cleanup(tmp); }
    }

    [Fact]
    public async Task Append_DayRollover_ResetsSums()
    {
        var (svc, _, tmp) = CreateService();
        try
        {
            await Task.Delay(500);
            await svc.AppendClosedBarAsync(Bar1Min(Day.AddHours(9), 100, 100, 100, 100, 10));
            var next = await svc.AppendClosedBarAsync(Bar1Min(Day.AddDays(1).AddHours(9), 200, 200, 200, 200, 5));
            Assert.NotNull(next);
            Assert.Equal(200.0, next!.Vwap, precision: 6);
            Assert.Equal(5, next.SumV);
            Assert.Single(svc.GetHistory());
        }
        finally { Cleanup(tmp); }
    }

    [Fact]
    public async Task RebuildFromCandles_ReconstructsDayHistory()
    {
        var (svc, keeper, tmp) = CreateService();
        try
        {
            await Task.Delay(500);
            await keeper.WriteDataAsync(
                $"WINFUT_{nameof(CandleService)}_1MIN_{Day:yyyy-MM-dd}.json",
                new List<BarStorageItem>
                {
                    Bar1Min(Day.AddHours(9), 100, 100, 100, 100, 10),
                    Bar1Min(Day.AddHours(9).AddMinutes(1), 110, 110, 110, 110, 10),
                });

            await svc.RebuildFromCandlesAsync(Day);

            var history = svc.GetHistory();
            Assert.Equal(2, history.Count);
            Assert.Equal(100.0, history[0].Vwap, precision: 6);
            Assert.Equal(105.0, history[1].Vwap, precision: 6);
        }
        finally { Cleanup(tmp); }
    }

    [Fact]
    public async Task Snapshot_ReturnsLastPoint()
    {
        var svc = await SettledService();
        Assert.Null(svc.GetSnapshot());
        await svc.AppendClosedBarAsync(Bar1Min(Day.AddHours(9), 100, 100, 100, 100, 10));
        await svc.AppendClosedBarAsync(Bar1Min(Day.AddHours(9).AddMinutes(1), 110, 110, 110, 110, 10));
        var snap = svc.GetSnapshot();
        Assert.NotNull(snap);
        Assert.Equal(Day.AddHours(9).AddMinutes(1), snap!.Minute);
        Assert.Equal(105.0, snap.Vwap, precision: 6);
    }
}
