using B3WM.Services.Market;
using B3WM.Services.Screen;
using B3WM.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace B3WM.Tests;

/// <summary>
/// Resolução da janela do automode no backend: manual passa direto; auto
/// delega ao StructureWindowResolver; a janela DIÁRIA é cacheada e só é
/// recalculada quando chega um print 1440 novo (chave = última data 1440).
/// </summary>
public class MarketWindowServiceTests
{
    private const string Sym = "WINFUT";

    private static MarketWindowService Build(IMarketData market)
    {
        var services = new ServiceCollection();
        services.AddSingleton(market);
        var sp = services.BuildServiceProvider();
        return new MarketWindowService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MarketWindowService>.Instance);
    }

    private static BarStorageItem Bar(DateTime date, double high, double low, int tf) => new()
    {
        Symbol = Sym,
        TimeFrame = tf,
        Date = date,
        Open = low,
        High = high,
        Low = low,
        Close = high,
        Volume = 10,
    };

    private static StructureStorageItem Struct(DateTime date, double up, double down, int tf) => new()
    {
        Symbol = Sym,
        TimeFrame = tf,
        Date = date,
        UpBorder = up,
        DownBorder = down,
        UpAuxBorder = up,
        DownAuxBorder = down,
    };

    [Fact]
    public async Task Manual_PassaDatasDireto_ESemConsultarDados()
    {
        var market = new Mock<IMarketData>(MockBehavior.Strict);
        var service = Build(market.Object);

        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 10, 17);
        var result = await service.ResolveAsync(new MarketWindowRequest
        {
            Symbol = Sym,
            TimeFrame = 5,
            ProfileAuto = false,
            ProfileFrom = from,
            ProfileTo = to,
            DailyAuto = false,
            DailyFrom = from,
            DailyTo = to,
        });

        Assert.False(result.Intraday.Auto);
        Assert.Equal(from, result.Intraday.From);
        Assert.Equal(to, result.Intraday.To);
        Assert.False(result.Daily.Auto);
        Assert.Equal(from, result.Daily.From);
        market.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Auto_Intraday_UsaResolverSobreBarras()
    {
        var today = DateTime.Today;
        var bars = new List<BarStorageItem>
        {
            Bar(today.AddHours(9), 100, 90, 5),
            Bar(today.AddHours(9).AddMinutes(5), 110, 95, 5),
            Bar(today.AddHours(9).AddMinutes(10), 105, 92, 5),
        };
        var structures = new List<StructureStorageItem> { Struct(today.AddHours(9), 100, 90, 5) };

        var market = new Mock<IMarketData>();
        market.Setup(m => m.CandlesAsync(Sym, 5, It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bars);
        market.Setup(m => m.StructuresAsync(Sym, 5, It.IsAny<double>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(structures);
        market.Setup(m => m.DailyStructuresAsync(Sym, It.IsAny<double>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StructureStorageItem>());
        market.Setup(m => m.DailyCandlesAsync(Sym, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BarStorageItem>());

        var service = Build(market.Object);
        var result = await service.ResolveAsync(new MarketWindowRequest
        {
            Symbol = Sym,
            TimeFrame = 5,
            ProfileAuto = true,
            DailyAuto = true,
        });

        Assert.True(result.Intraday.Auto);
        Assert.Equal(bars[0].Date, result.Intraday.From);
        Assert.Equal(bars[^1].Date, result.Intraday.To);
    }

    [Fact]
    public async Task Auto_Daily_CacheiaAteSurdirPrint1440Novo()
    {
        var today = DateTime.Today;
        var structDate = today.AddDays(-3);
        var dailyStructs = new List<StructureStorageItem> { Struct(structDate, 100, 90, 1440) };
        var dailyBars = new List<BarStorageItem> { Bar(today.AddDays(-1), 110, 95, 1440) };

        var market = new Mock<IMarketData>();
        market.Setup(m => m.CandlesAsync(Sym, 5, It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BarStorageItem>());
        market.Setup(m => m.StructuresAsync(Sym, 5, It.IsAny<double>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StructureStorageItem>());
        market.Setup(m => m.DailyStructuresAsync(Sym, It.IsAny<double>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => dailyStructs);
        market.Setup(m => m.DailyCandlesAsync(Sym, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => dailyBars);

        var service = Build(market.Object);
        var request = new MarketWindowRequest { Symbol = Sym, TimeFrame = 5, ProfileAuto = true, DailyAuto = true };

        // 1ª e 2ª resoluções: cache hit na 2ª (DailyCandles só 1x).
        await service.ResolveAsync(request);
        await service.ResolveAsync(request);
        market.Verify(m => m.DailyCandlesAsync(Sym, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);

        // Print 1440 novo → cache invalidado (DailyCandles 2ª vez).
        dailyStructs.Add(Struct(today.AddDays(-1), 115, 90, 1440));
        await service.ResolveAsync(request);
        market.Verify(m => m.DailyCandlesAsync(Sym, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
