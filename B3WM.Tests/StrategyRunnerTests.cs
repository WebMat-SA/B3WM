using System.Text.Json;
using System.Text.Json.Serialization;
using B3WM.Controllers;
using B3WM.Services.Strategies;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Strategies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace B3WM.Tests;

public sealed class StubStrategy : StrategyBase
{
    public StubStrategy() : base(new Mock<ILogger<StubStrategy>>().Object) { }
    public override string Name => "Stub";
    public override string DetailsMd => "## Stub\nStrategy de teste.";
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
    public void ShouldExecuteOnBar_SessionTimeFrameOnly()
    {
        StrategySession Session(int tf) => new() { Strategy = "JevAnalysis", Symbol = "WINFUT", TriggerTimeFrame = tf };
        BarStorageItem Bar(int tf) => new()
            { Date = new DateTime(2026, 10, 6, 10, 6, 0), Symbol = "WINFUT", TimeFrame = tf, Open = 100 };
        Assert.True(StrategyRunner.ShouldExecuteOnBar(Session(2), Bar(2)));
        Assert.False(StrategyRunner.ShouldExecuteOnBar(Session(2), Bar(5)));
        Assert.True(StrategyRunner.ShouldExecuteOnBar(Session(5), Bar(5)));
        Assert.False(StrategyRunner.ShouldExecuteOnBar(Session(2), Bar(1440)));
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
    public void MergeLogDay_EmptyIncomingPreservesExisting()
    {
        // STOP vazio (sessão nova sem decisões) não pode apagar o dia:
        // PersistLogAsync faz merge — existente com dados + vazio = preserva.
        var day = new DateTime(2026, 10, 16);
        var existing = new B3WM.Shared.Models.Strategies.StrategyLogDay
        {
            Date = day, Symbol = "WINFUT", Strategy = "JevAnalysis", SnapshotHash = "manha",
            Decisions = new()
            {
                new() { Kind = "avaliacao", At = new DateTime(2026, 10, 16, 11, 28, 24), Time = "11:28", Event = "bubble", Side = "manter", SnapshotHash = "manha", PositionBefore = "flat", Action = "manter" },
            },
        };
        var emptyStop = new B3WM.Shared.Models.Strategies.StrategyLogDay
        {
            Date = day, Symbol = "WINFUT", Strategy = "JevAnalysis", SnapshotHash = "tarde",
        };
        var merged = StrategyRunner.MergeLogDay(existing, emptyStop);
        Assert.Single(merged.Decisions);
        Assert.Equal("manha", merged.SnapshotHash);
    }

    private sealed class TempKeeper : B3WM.Services.DataKeeperBase
    {
        private readonly string _root;
        public TempKeeper(string root) => _root = root;
        protected override string RootDirectory => _root;
    }

    [Fact]
    public async Task ManualTrigger_BypassesFilterPersistsAndRepeats()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "b3wm-manual-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmp);
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<B3WM.Services.DataKeeperBase>(new TempKeeper(tmp));
            services.AddSingleton<IStrategy>(new StubStrategy());
            using var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var runner = new StrategyRunner(
                scopeFactory,
                Enumerable.Empty<B3WM.Services.Core.OrchestratorService>(),
                new Mock<ILogger<StrategyRunner>>().Object);

            var id = runner.Arm("Stub", "WINFUT", 2, "{}", Filter(), "manualhash");
            var at = DateTime.Today.AddHours(11).AddMinutes(5);
            var bubble = Bubble(120, 900, 209770, at); // agente 120 fora do filtro

            Assert.Equal(ManualTriggerStatus.SessionNotFound,
                await runner.ManualTriggerAsync("nope", bubble));

            var otherSymbol = Bubble(120, 900, 209770, at);
            otherSymbol.Symbol = "WDOFUT";
            Assert.Equal(ManualTriggerStatus.SymbolMismatch,
                await runner.ManualTriggerAsync(id, otherSymbol));

            Assert.True(runner.Pause(id, true));
            Assert.Equal(ManualTriggerStatus.Paused,
                await runner.ManualTriggerAsync(id, bubble));
            Assert.True(runner.Pause(id, false));

            // O filtro reprovaria a bubble no trigger automático; manual dispara.
            Assert.Equal(ManualTriggerStatus.Fired,
                await runner.ManualTriggerAsync(id, bubble));
            var state = Assert.Single(runner.States());
            Assert.Equal(1, state.Decisions);
            Assert.Equal("manual", state.ReportItems[0].Event);
            Assert.False(string.IsNullOrWhiteSpace(state.ReportItems[0].TriggerId));

            // Reenviar a mesma bubble é um novo log (TriggerId único), não dedupe.
            Assert.Equal(ManualTriggerStatus.Fired,
                await runner.ManualTriggerAsync(id, bubble));
            Assert.Equal(2, runner.States()[0].Decisions);

            var path = Path.Combine(tmp, $"WINFUT_Strategy_Stub_{DateTime.Today:yyyy-MM-dd}.json");
            Assert.True(File.Exists(path));
            var json = await File.ReadAllTextAsync(path);
            var log = System.Text.Json.JsonSerializer.Deserialize<B3WM.Shared.Models.Strategies.StrategyLogDay>(json);
            Assert.NotNull(log);
            Assert.Equal(2, log!.Decisions.Count);
            Assert.All(log.Decisions, d => Assert.Equal("manual", d.Event));
            Assert.Equal(2, log.Decisions.Select(d => d.TriggerId).Distinct().Count());
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public async Task FlushCadaDecisao_PersisteNaPrimeiraAvaliacao()
    {
        // Regressão do pico de energia 2026-10-16: 8 JEVs em RAM (<10) foram
        // perdidos porque só persistia a cada 10. Agora a 1ª já vai a disco.
        var tmp = Path.Combine(Path.GetTempPath(), "b3wm-flush-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmp);
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<B3WM.Services.DataKeeperBase>(new TempKeeper(tmp));
            services.AddSingleton<IStrategy>(new StubStrategy());
            using var provider = services.BuildServiceProvider();
            var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var runner = new StrategyRunner(
                scopeFactory,
                Enumerable.Empty<B3WM.Services.Core.OrchestratorService>(),
                new Mock<ILogger<StrategyRunner>>().Object);

            runner.Arm("Stub", "WINFUT", 2, "{}", Filter(), "flushash");
            var t0 = DateTime.Today.AddHours(11).AddMinutes(28);
            await runner.PublishAsync(new BubbleAppeared
            {
                Symbol = "WINFUT", At = t0,
                Item = Bubble(122, 500, 209770, t0),
            }, "bubble");

            var path = Path.Combine(tmp, $"WINFUT_Strategy_Stub_{DateTime.Today:yyyy-MM-dd}.json");
            Assert.True(File.Exists(path));
            var json = await File.ReadAllTextAsync(path);
            var log = System.Text.Json.JsonSerializer.Deserialize<B3WM.Shared.Models.Strategies.StrategyLogDay>(json);
            Assert.NotNull(log);
            Assert.Single(log!.Decisions);

            // 2ª bubble em bucket diferente acumula (merge, sem duplicar).
            var t1 = t0.AddMinutes(2);
            await runner.PublishAsync(new BubbleAppeared
            {
                Symbol = "WINFUT", At = t1,
                Item = Bubble(122, 500, 209800, t1),
            }, "bubble");
            json = await File.ReadAllTextAsync(path);
            log = System.Text.Json.JsonSerializer.Deserialize<B3WM.Shared.Models.Strategies.StrategyLogDay>(json);
            Assert.NotNull(log);
            Assert.Equal(2, log!.Decisions.Count);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
    }
    [Fact]
    public void MergeLogDay_AccumulatesSessionsWithoutDuplicates()
    {
        var day = new DateTime(2026, 10, 13);
        var first = new B3WM.Shared.Models.Strategies.StrategyLogDay
        {
            Date = day, Symbol = "WINFUT", Strategy = "JevAnalysis", SnapshotHash = "aaa",
            Decisions = new()
            {
                new() { Kind = "avaliacao", At = new DateTime(2026, 10, 13, 10, 0, 0), Time = "10:00", Event = "bubble", Side = "manter", SnapshotHash = "aaa", PositionBefore = "flat", Action = "manter" },
            },
        };
        var second = new B3WM.Shared.Models.Strategies.StrategyLogDay
        {
            Date = day, Symbol = "WINFUT", Strategy = "JevAnalysis", SnapshotHash = "bbb",
            Decisions = new()
            {
                // Duplicada (mesmo At/side/action/snapshot): não entra 2x.
                new() { Kind = "avaliacao", At = new DateTime(2026, 10, 13, 10, 0, 0), Time = "10:00", Event = "bubble", Side = "manter", SnapshotHash = "aaa", PositionBefore = "flat", Action = "manter" },
                new() { Kind = "avaliacao", At = new DateTime(2026, 10, 13, 15, 43, 0), Time = "15:43", Event = "bubble", Side = "vender", SnapshotHash = "bbb", PositionBefore = "flat", Action = "manter" },
            },
            PaperTrades = new()
            {
                new() { Side = "comprar", EntryAt = new DateTime(2026, 10, 13, 15, 44, 0), EntryTime = "15:44", Entry = 100, ExitAt = new DateTime(2026, 10, 13, 15, 46, 0), ExitTime = "15:46", Exit = 110, Pts = 10, ExitReason = "sinal encerrar", EntryConf = 0.7 },
            },
        };
        var merged = StrategyRunner.MergeLogDay(first, second);
        Assert.Equal(2, merged.Decisions.Count);
        // Ordenado por At (sessão da manhã antes da tarde).
        Assert.Equal(new DateTime(2026, 10, 13, 10, 0, 0), merged.Decisions[0].At);
        Assert.Equal(new DateTime(2026, 10, 13, 15, 43, 0), merged.Decisions[1].At);
        Assert.Single(merged.PaperTrades);
        Assert.Equal(10, merged.RealizedPts);
        // Snapshot do dia segue o da primeira sessão.
        Assert.Equal("aaa", merged.SnapshotHash);
    }

    [Fact]
    public async Task ManualTrigger_Response_SerializesStatus_WithReadOnlyPropsIgnored()
    {
        // Regressão: o JSON do MVC ignora propriedades read-only e, com
        // objeto anônimo, a resposta virava "{}" — o app via status null.
        var tmp = Path.Combine(Path.GetTempPath(), "b3wm-resp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmp);
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<B3WM.Services.DataKeeperBase>(new TempKeeper(tmp));
            services.AddSingleton<IStrategy>(new StubStrategy());
            using var provider = services.BuildServiceProvider();
            var runner = new StrategyRunner(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Enumerable.Empty<B3WM.Services.Core.OrchestratorService>(),
                new Mock<ILogger<StrategyRunner>>().Object);
            var keeper = provider.GetRequiredService<B3WM.Services.DataKeeperBase>();
            var controller = new StrategyController(
                new StrategyRegistry(new IStrategy[] { new StubStrategy() }),
                runner, keeper, new Mock<ILogger<StrategyController>>().Object);

            var id = runner.Arm("Stub", "WINFUT", 2, "{}", Filter(), "manualhash");
            var at = DateTime.Today.AddHours(11).AddMinutes(5);
            var result = await controller.ManualTrigger(new StrategyManualTriggerRequest
            {
                SessionId = id,
                Bubble = Bubble(120, 900, 209770, at),
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            var options = new JsonSerializerOptions
            {
                IgnoreReadOnlyProperties = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
            };
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, options));
            Assert.Equal("fired", doc.RootElement.GetProperty("status").GetString());
            Assert.True(doc.RootElement.TryGetProperty("session", out _));
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { }
        }
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
                new() { Kind = "avaliacao", At = new DateTime(2026, 10, 6, 10, 4, 0), Time = "10:04", Event = "candle_close", Side = "comprar", Confidence = 0.85, ShouldTrade = true, SnapshotHash = "abc123", PositionBefore = "flat", Action = "sinal abre comprar (conf 0.85)" },
                new() { Kind = "execucao", At = new DateTime(2026, 10, 6, 10, 6, 0), Time = "10:06", Event = "exec", Side = "comprar", SnapshotHash = "abc123", PositionBefore = "flat", Action = "exec abre comprar @189230" },
            },
            PaperTrades = new()
            {
                new() { Side = "comprar", EntryAt = new DateTime(2026, 10, 6, 10, 6, 0), EntryTime = "10:06", Entry = 189230, ExitAt = new DateTime(2026, 10, 6, 10, 8, 0), ExitTime = "10:08", Exit = 189300, Pts = 70, ExitReason = "sinal encerrar", EntryConf = 0.85 },
            },
            RealizedPts = 915,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(log);
        var back = System.Text.Json.JsonSerializer.Deserialize<B3WM.Shared.Models.Strategies.StrategyLogDay>(json);
        Assert.NotNull(back);
        Assert.Equal(2, back!.Decisions.Count);
        Assert.Equal("exec abre comprar @189230", back.Decisions[1].Action);
        Assert.Equal("avaliacao", back.Decisions[0].Kind);
        // Data completa canônica p/ timeline X.
        Assert.Equal(new DateTime(2026, 10, 6, 10, 4, 0), back.Decisions[0].At);
        Assert.Equal(new DateTime(2026, 10, 6, 10, 6, 0), back.Decisions[1].At);
        Assert.Single(back.PaperTrades);
        Assert.Equal(new DateTime(2026, 10, 6, 10, 6, 0), back.PaperTrades[0].EntryAt);
        Assert.Equal(new DateTime(2026, 10, 6, 10, 8, 0), back.PaperTrades[0].ExitAt);
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
        Enumerable.Empty<B3WM.Services.Core.VwapService>(),
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
