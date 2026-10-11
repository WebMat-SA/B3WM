using B3WM.Services.Strategies;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace B3WM.Tests;

public sealed class StubStrategy : StrategyBase
{
    public StubStrategy() : base(new Mock<ILogger<StubStrategy>>().Object) { }
    public override string Name => "Stub";
    public override bool ShouldTrigger(StrategyEvent ev, StrategyTriggerContext ctx) =>
        TriggerChecks.IsVisibleBubble(ev, ctx.Filter);
    public override Task<StrategyDecision?> EvaluateAsync(
        StrategyEvent ev, StrategyContext ctx, CancellationToken ct = default) =>
        Task.FromResult<StrategyDecision?>(new StrategyDecision { Side = "manter" });
}

public class StrategyRunnerTests
{

    private static StrategyRunner Runner() => new(
        new Mock<IServiceScopeFactory>().Object,
        Enumerable.Empty<B3WM.Services.Core.OrchestratorService>(),
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
    public void ShouldTrigger_BubbleOnly_NoCandleClose()
    {
        var s = new StubStrategy();
        BarStorageItem Bar(int tf) => new()
            { Date = DateTime.Now, Symbol = "WINFUT", TimeFrame = tf };
        // Somente-bubble: candle nunca dispara, mesmo no TF da tela.
        Assert.False(s.ShouldTrigger(
            new CandleClosed { Symbol = "WINFUT", At = DateTime.Now, Bar = Bar(5) }, Tctx(5)));
        Assert.False(s.ShouldTrigger(
            new CandleClosed { Symbol = "WINFUT", At = DateTime.Now, Bar = Bar(2) }, Tctx(5)));
        // Bubble visível (agente 122 + amount 500 ≥ 475) dispara.
        Assert.True(s.ShouldTrigger(
            new BubbleAppeared { Symbol = "WINFUT", At = DateTime.Now, Item = Bubble(122, 500, 100, DateTime.Now) },
            Tctx(5)));
        // Bubble fora do filtro não dispara.
        Assert.False(s.ShouldTrigger(
            new BubbleAppeared { Symbol = "WINFUT", At = DateTime.Now, Item = Bubble(122, 100, 100, DateTime.Now) },
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
    public void EvaluationTime_CandleCloseVsBubble()
    {
        var open = new DateTime(2026, 10, 6, 10, 4, 0);
        var atClose = StrategyRunner.EvaluationTime(new CandleClosed
        {
            Symbol = "WINFUT",
            At = open,
            Bar = new BarStorageItem { Date = open, Symbol = "WINFUT", TimeFrame = 2 },
        });
        // Candle 2min aberto às 10:04 foi avaliado no fechamento, às 10:06.
        Assert.Equal(new DateTime(2026, 10, 6, 10, 6, 0), atClose);

        var bubbleAt = new DateTime(2026, 10, 6, 10, 5, 30);
        var atBubble = StrategyRunner.EvaluationTime(new BubbleAppeared
        {
            Symbol = "WINFUT",
            At = bubbleAt,
            Item = Bubble(122, 500, 100, bubbleAt),
        });
        Assert.Equal(bubbleAt, atBubble);
    }

    [Fact]
    public void BubbleBucket_UsesBubbleTimeAndSessionTimeFrame()
    {
        // TF2: 10:04:00 e 10:05:59 caem no bucket 10:04; 10:06:00 abre bucket novo.
        Assert.Equal(new DateTime(2026, 10, 6, 10, 4, 0),
            StrategyRunner.BubbleBucketFor(new DateTime(2026, 10, 6, 10, 4, 0), 2));
        Assert.Equal(new DateTime(2026, 10, 6, 10, 4, 0),
            StrategyRunner.BubbleBucketFor(new DateTime(2026, 10, 6, 10, 5, 59), 2));
        Assert.Equal(new DateTime(2026, 10, 6, 10, 6, 0),
            StrategyRunner.BubbleBucketFor(new DateTime(2026, 10, 6, 10, 6, 0), 2));
        // Mesmo instante, TF5 alinha diferente: 10:04:59 e 10:06:30 estão no bucket 10:05.
        Assert.Equal(new DateTime(2026, 10, 6, 10, 0, 0),
            StrategyRunner.BubbleBucketFor(new DateTime(2026, 10, 6, 10, 4, 59), 5));
        Assert.Equal(new DateTime(2026, 10, 6, 10, 5, 0),
            StrategyRunner.BubbleBucketFor(new DateTime(2026, 10, 6, 10, 6, 30), 5));
    }

    [Fact]
    public void BubbleBucketGate_FirstPerBucketOnly()
    {
        var bucket = new DateTime(2026, 10, 6, 10, 4, 0);
        // Sessão nova (sem bucket): dispara.
        Assert.True(StrategyRunner.IsNewBubbleBucket(null, bucket));
        // Mesmo bucket já avaliado: não dispara (2ª bubble do bucket é skip).
        Assert.False(StrategyRunner.IsNewBubbleBucket(bucket, bucket));
        // Bucket seguinte: dispara de novo.
        Assert.True(StrategyRunner.IsNewBubbleBucket(bucket, bucket.AddMinutes(2)));
    }

    [Fact]
    public void BubbleBucketGate_DifferentTimeFramesFireOnDifferentBubbles()
    {
        // Mesma bubble (10:05:30): @2 está no bucket 10:04, @5 no bucket 10:05.
        var at = new DateTime(2026, 10, 6, 10, 5, 30);
        var b2 = StrategyRunner.BubbleBucketFor(at, 2);
        var b5 = StrategyRunner.BubbleBucketFor(at, 5);
        Assert.NotEqual(b2, b5);
        // Simulando o gate do PublishAsync por sessão:
        DateTime? last2 = null, last5 = null;
        Assert.True(StrategyRunner.IsNewBubbleBucket(last2, b2)); last2 = b2;
        Assert.True(StrategyRunner.IsNewBubbleBucket(last5, b5)); last5 = b5;
        // 2ª bubble no mesmo instante/bucket: ambas as sessões pulam.
        Assert.False(StrategyRunner.IsNewBubbleBucket(last2, StrategyRunner.BubbleBucketFor(at, 2)));
        Assert.False(StrategyRunner.IsNewBubbleBucket(last5, StrategyRunner.BubbleBucketFor(at, 5)));
        // Bubble seguinte (10:06:10) abre bucket novo só p/ @2 (10:06); @5 segue em 10:05.
        var next = new DateTime(2026, 10, 6, 10, 6, 10);
        Assert.True(StrategyRunner.IsNewBubbleBucket(last2, StrategyRunner.BubbleBucketFor(next, 2)));
        Assert.False(StrategyRunner.IsNewBubbleBucket(last5, StrategyRunner.BubbleBucketFor(next, 5)));
    }

    [Fact]
    public void SnapshotHash_Deterministic()
    {
        Assert.Equal(StrategyRunner.SnapshotHashOf("{}"), StrategyRunner.SnapshotHashOf("{}"));
        Assert.NotEqual(StrategyRunner.SnapshotHashOf("{}"), StrategyRunner.SnapshotHashOf("{ }"));
        Assert.Equal(12, StrategyRunner.SnapshotHashOf("{}").Length);
    }

    [Fact]
    public void DescribeEvaluation_PaperRule()
    {
        var buy = new StrategyDecision { Side = "comprar", Confidence = 0.85, ShouldTrade = true };
        var (a1, e1, x1) = StrategyRunner.DescribeEvaluation(buy, hasPosition: false, exitThr: 0.5);
        Assert.Equal("sinal abre comprar (conf 0.85)", a1);
        Assert.Equal("comprar", e1);
        Assert.False(x1);

        var hold = new StrategyDecision { Side = "manter", Confidence = 0.4 };
        var (a2, e2, x2) = StrategyRunner.DescribeEvaluation(hold, hasPosition: false, exitThr: 0.5);
        Assert.Equal("manter", a2);
        Assert.Null(e2);
        Assert.False(x2);

        var exit = new StrategyDecision { Side = "manter", Encerrar = 0.7 };
        var (a3, e3, x3) = StrategyRunner.DescribeEvaluation(exit, hasPosition: true, exitThr: 0.5);
        Assert.Equal("sinal fecha (encerrar 0.70)", a3);
        Assert.Null(e3);
        Assert.True(x3);

        var holdPos = new StrategyDecision { Side = "comprar", Encerrar = 0.2 };
        var (a4, _, x4) = StrategyRunner.DescribeEvaluation(holdPos, hasPosition: true, exitThr: 0.5);
        Assert.Equal("manter", a4);
        Assert.False(x4);
    }

    [Fact]
    public void DecisionLog_Serializes()
    {
        var log = new B3WM.Shared.Models.Strategies.StrategyLogDay
        {
            Date = new DateTime(2026, 10, 6),
            Symbol = "WINFUT",
            Strategy = "JevAnalysis",
            SnapshotHash = "abc123",
            Decisions = new()
            {
                new() { Kind = "avaliacao", Time = "10:04", Event = "candle_close", Side = "comprar", Confidence = 0.85, ShouldTrade = true, SnapshotHash = "abc123", PositionBefore = "flat", Action = "sinal abre comprar (conf 0.85)" },
                new() { Kind = "execucao", Time = "10:06", Event = "exec", Side = "comprar", SnapshotHash = "abc123", PositionBefore = "flat", Action = "exec abre comprar @189230" },
            },
            RealizedPts = 915,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(log);
        var back = System.Text.Json.JsonSerializer.Deserialize<B3WM.Shared.Models.Strategies.StrategyLogDay>(json);
        Assert.NotNull(back);
        Assert.Equal(2, back!.Decisions.Count);
        Assert.Equal("exec abre comprar @189230", back.Decisions[1].Action);
        Assert.Equal("avaliacao", back.Decisions[0].Kind);
    }
}

public class OrchestratorTriggerTests
{
    private static B3WM.Services.Core.OrchestratorService Orchestrator() => new(
        "WINFUT",
        new Mock<IHubContext<B3WM.Services.DataHub, B3WM.Shared.Interfaces.IDataHubClient>>().Object,
        Enumerable.Empty<B3WM.Services.Core.CandleService>(),
        Enumerable.Empty<B3WM.Services.Core.BubbleService>(),
        Enumerable.Empty<B3WM.Services.Core.VolumeService>(),
        Enumerable.Empty<B3WM.Services.Core.StructureService>(),
        Enumerable.Empty<B3WM.Services.Core.AdjustmentForecastService>(),
        new Mock<ILogger<B3WM.Services.Core.OrchestratorService>>().Object);

    [Fact]
    public void Orchestrator_ExposesBubbleAndCandleClosed_ForRunner()
    {
        using var o = Orchestrator();
        o.OnBubble += _ => Task.CompletedTask;
        o.OnCandleClosed += (_, _) => Task.CompletedTask;
        // Runner consome o orchestrator (padrão Core→Strategies).
        var r = new StrategyRunner(
            new Mock<IServiceScopeFactory>().Object,
            new[] { o },
            new Mock<ILogger<StrategyRunner>>().Object);
        Assert.Empty(r.States());
    }

    [Fact]
    public void Orchestrator_Dispose_UnsubscribesCleanly()
    {
        var o = Orchestrator();
        var ex = Record.Exception(() => o.Dispose());
        Assert.Null(ex);
    }
}
