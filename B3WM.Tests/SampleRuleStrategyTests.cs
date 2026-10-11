using B3WM.Services.Market;
using B3WM.Services.Strategies;
using B3WM.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace B3WM.Tests;

/// <summary>
/// Prova de DX: strategy rule-based completa em ~30 linhas, usando SÓ
/// IMarketData mockado (sem builder, snapshot, runner ou Jev).
/// Regra (somente-bubble): compra se houver mais compras que vendas com
/// amount ≥ threshold na janela do TF da sessão; nunca vende (demonstração).
/// </summary>
public class SampleRuleStrategyTests
{
    public sealed class SampleBuyPressureStrategy : StrategyBase
    {
        private readonly IMarketData _market;
        public SampleBuyPressureStrategy(ILogger<SampleBuyPressureStrategy> logger, IMarketData market)
            : base(logger) => _market = market;

        public override string Name => "SampleBuyPressure";
        public override string Description => "Exemplo rule-based (teste): compra em pressão compradora de bolhas";
        public override bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx) =>
            TriggerChecks.IsVisibleBubble(ev, ctx.Filter);

        /// <summary>Config 100% no código (nada no tab).</summary>
        private const int MinAmount = 250;

        public override async Task<StrategyDecision?> EvaluateAsync(
            StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default)
        {
            if (ev is not BubbleAppeared) return null;
            var tf = ctx.TimeFrame <= 0 ? 2 : ctx.TimeFrame;
            var bubbles = await _market.BubblesAsync(ctx.Symbol, null, ct);
            var fresh = bubbles
                .Where(b => b.Date > ev.At.AddMinutes(-tf) && b.Date <= ev.At)
                .ToList();
            var buys = fresh.Where(b => b.ActionType == B3WM.Shared.Entity.Ticks2.ActionType.Buy && b.Amount >= MinAmount).ToList();
            var sells = fresh.Where(b => b.ActionType != B3WM.Shared.Entity.Ticks2.ActionType.Buy && b.Amount >= MinAmount).ToList();
            if (buys.Count > sells.Count && buys.Count > 0)
                return new StrategyDecision { Side = "comprar", Confidence = 0.6, ShouldTrade = true, Reason = $"{buys.Count}xb{sells.Count}s" };
            return new StrategyDecision { Side = "manter", Confidence = 0.5, Reason = $"{buys.Count}xb{sells.Count}s" };
        }
    }

    private static BubbleStorageItem Bubble(int agent, decimal amount, bool buy, DateTime date) => new()
    {
        Agent = agent, Amount = amount, Price = 100, Date = date,
        ActionType = buy ? B3WM.Shared.Entity.Ticks2.ActionType.Buy : B3WM.Shared.Entity.Ticks2.ActionType.Sale,
        Symbol = "WINFUT",
    };

    [Fact]
    public async Task SampleStrategy_BuyPressure()
    {
        var t = new DateTime(2026, 10, 2, 10, 4, 0);
        var market = new Mock<IMarketData>();
        market.Setup(m => m.BubblesAsync("WINFUT", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BubbleStorageItem>
            {
                Bubble(1, 300, true, t.AddSeconds(-30)),
                Bubble(2, 400, true, t.AddSeconds(-10)),
                Bubble(3, 100, false, t.AddSeconds(-5)),
            });
        var s = new SampleBuyPressureStrategy(
            new Mock<ILogger<SampleBuyPressureStrategy>>().Object, market.Object);
        var decision = await s.EvaluateAsync(
            new BubbleAppeared
            {
                Symbol = "WINFUT", At = t,
                Item = Bubble(2, 400, true, t.AddSeconds(-10)),
            },
            new StrategyContext
            {
                Symbol = "WINFUT",
                TimeFrame = 2,
            });
        Assert.NotNull(decision);
        Assert.Equal("comprar", decision!.Side);
        Assert.True(decision.ShouldTrade);
    }

    [Fact]
    public async Task SampleStrategy_TriggerContract()
    {
        var s = new SampleBuyPressureStrategy(
            new Mock<ILogger<SampleBuyPressureStrategy>>().Object,
            new Mock<IMarketData>().Object);
        Assert.Equal("SampleBuyPressure", s.Name);
        // Somente-bubble: bubble dispara (filtro null = passa), candle nunca.
        Assert.True(s.ShouldTrigger(
            new BubbleAppeared { Symbol = "WINFUT", At = DateTime.Now, Item = Bubble(1, 300, true, DateTime.Now) },
            new StrategyTriggerContext { Symbol = "WINFUT", TimeFrame = 2 }));
        Assert.False(s.ShouldTrigger(
            new CandleClosed { Symbol = "WINFUT", At = DateTime.Now,
                Bar = new BarStorageItem { Date = DateTime.Now, Symbol = "WINFUT", TimeFrame = 2 } },
            new StrategyTriggerContext { Symbol = "WINFUT", TimeFrame = 2 }));
        s.Reset(); // não explode
        Assert.Null(await s.EvaluateAsync(
            new CandleClosed { Symbol = "WINFUT", At = DateTime.Now,
                Bar = new BarStorageItem { Date = DateTime.Now, Symbol = "WINFUT", TimeFrame = 2 } },
            new StrategyContext { Symbol = "WINFUT", TimeFrame = 2 }));
    }
}
