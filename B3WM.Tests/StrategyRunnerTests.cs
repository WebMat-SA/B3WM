using B3WM.Services.Strategies;
using B3WM.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace B3WM.Tests;

public sealed class StubStrategy : StrategyBase
{
    public StubStrategy() : base(new Mock<ILogger<StubStrategy>>().Object) { }
    public override string Name => "Stub";
    public override bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx) =>
        TriggerChecks.IsCandleClose(ev, ctx.TimeFrame);
    public override Task<StrategyDecision?> EvaluateAsync(
        StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default) =>
        Task.FromResult<StrategyDecision?>(new StrategyDecision { Side = "manter" });
}

public class StrategyRunnerTests
{

    private static StrategyRunner Runner() => new(
        new Mock<IServiceScopeFactory>().Object,
        Enumerable.Empty<B3WM.Services.Core.CandleService>(),
        Enumerable.Empty<B3WM.Services.Core.BubbleService>(),
        new Mock<ILogger<StrategyRunner>>().Object);

    private static BubbleStorageItem Bubble(int agent, decimal amount, double price, DateTime date) => new()
    {
        Agent = agent, Amount = amount, Price = price, Date = date,
        ActionType = B3WM.Shared.Entity.Ticks2.ActionType.Buy, Symbol = "WINFUT",
    };

    private static BubbleFilter Filter() => new()
    {
        Enabled = true,
        Threshold = 475,
        PerAgent = new Dictionary<int, int> { [114] = 700 },
        Agents = new HashSet<int> { 122, 127 },
        AmountFilter = true,
        AgentsFilter = true,
    };

    private static StrategyTriggerContext Tctx(int tf = 2) => new()
    {
        Symbol = "WINFUT", TimeFrame = tf, Filter = Filter(),
    };

    [Fact]
    public void ShouldTrigger_CandleClose_UsesScreenTimeFrame()
    {
        var s = new StubStrategy();
        BarStorageItem Bar(int tf) => new()
            { Date = DateTime.Now, Symbol = "WINFUT", TimeFrame = tf };
        // Tela em 5min: só fecha de 5 dispara; tela em 2: só 2.
        Assert.True(s.ShouldTrigger(
            new CandleClosed { Symbol = "WINFUT", At = DateTime.Now, Bar = Bar(5) }, Tctx(5)));
        Assert.False(s.ShouldTrigger(
            new CandleClosed { Symbol = "WINFUT", At = DateTime.Now, Bar = Bar(2) }, Tctx(5)));
        Assert.False(s.ShouldTrigger(
            new BubbleAppeared { Symbol = "WINFUT", At = DateTime.Now, Item = Bubble(122, 500, 100, DateTime.Now) },
            Tctx(5)));
    }

    [Fact]
    public void TriggerChecks_BubbleFilter()
    {
        var ctx = Tctx();
        Assert.True(TriggerChecks.IsVisibleBubble(new BubbleAppeared
            { Symbol = "WINFUT", At = DateTime.Now, Item = Bubble(122, 500, 100, DateTime.Now) }, ctx.Filter));
        Assert.False(TriggerChecks.IsVisibleBubble(new BubbleAppeared
            { Symbol = "WINFUT", At = DateTime.Now, Item = Bubble(120, 900, 100, DateTime.Now) }, ctx.Filter)); // desmarcado
        Assert.False(TriggerChecks.IsVisibleBubble(new BubbleAppeared
            { Symbol = "WINFUT", At = DateTime.Now, Item = Bubble(122, 100, 100, DateTime.Now) }, ctx.Filter)); // < 475
        Assert.False(TriggerChecks.IsVisibleBubble(new CandleClosed
            { Symbol = "WINFUT", At = DateTime.Now, Bar = new BarStorageItem() }, ctx.Filter));
    }

    [Fact]
    public void SessionLifecycle_ArmPauseStop()
    {
        var r = Runner();
        var id = r.Arm("JevAnalysis", "WINFUT", 2, "{}", Filter(), "abc123");
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Single(r.States());
        Assert.True(r.Pause(id, true));
        Assert.True(r.States()[0].Paused);
        Assert.True(r.Pause(id, false));
        var log = r.Stop(id);
        Assert.NotNull(log);
        Assert.Equal("abc123", log!.SnapshotHash);
        Assert.Empty(r.States());
        Assert.False(r.Pause("nope", true));
        Assert.Null(r.Stop("nope"));
    }

    [Fact]
    public void SnapshotHash_Deterministic()
    {
        Assert.Equal(StrategyRunner.SnapshotHashOf("{}"), StrategyRunner.SnapshotHashOf("{}"));
        Assert.NotEqual(StrategyRunner.SnapshotHashOf("{}"), StrategyRunner.SnapshotHashOf("{ }"));
        Assert.Equal(12, StrategyRunner.SnapshotHashOf("{}").Length);
    }
}
