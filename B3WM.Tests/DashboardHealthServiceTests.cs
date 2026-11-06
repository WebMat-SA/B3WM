using B3WM.Services.Dashboard;
using B3WM.Shared.Entity;
using B3WM.Shared.Models.Dashboard;

namespace B3WM.Tests;

public class DashboardHealthServiceTests
{
    private static Ticks2 Tick(int buyer, int seller, string symbol = "WINFUT") => new()
    {
        Time = DateTime.Now,
        Value = 100,
        Volume = 10,
        Buyer = (Ticks2.Agents)buyer,
        Seller = (Ticks2.Agents)seller,
        Symbol = symbol,
    };

    [Fact]
    public void RecordTicks_CountsTicksAndDetectsUnknownAgents()
    {
        var health = new DashboardHealthService();

        health.RecordTicks("WINFUT", new[]
        {
            Tick(122, 999999), // 122 conhecido; 999999 desconhecido
            Tick(122, 114),    // ambos conhecidos
        });

        var metrics = health.GetSymbolMetrics("WINFUT");
        Assert.Equal(2, metrics.TotalTicks);
        Assert.Equal(2, metrics.TicksLastMinute);

        var unknown = Assert.Single(health.GetUnknownAgents());
        Assert.Equal(999999, unknown.AgentCode);
        Assert.Equal("tick", unknown.Source);
        Assert.Equal(1, unknown.Count);
        Assert.Contains("WINFUT", unknown.Symbols);
    }

    [Fact]
    public void RecordUnknownAgentNames_KeepsRawNameForExtractor()
    {
        var health = new DashboardHealthService();

        health.RecordUnknownAgentNames("WDOFUT", new[] { "NOVA CORRETORA", "OUTRA_CORRETORA" });
        health.RecordUnknownAgentNames("WDOFUT", new[] { "NOVA CORRETORA" });

        var unknown = health.GetUnknownAgents();
        Assert.Equal(2, unknown.Count);

        var nova = Assert.Single(unknown, u => u.RawNames.Contains("NOVA CORRETORA"));
        Assert.Equal(0, nova.AgentCode);
        Assert.Equal("extractor", nova.Source);
        Assert.Equal(2, nova.Count);
        Assert.Contains("WDOFUT", nova.Symbols);
    }

    [Fact]
    public void RecordBubbleAgent_OnlyUnknownAgentsAreReported()
    {
        var health = new DashboardHealthService();

        health.RecordBubbleAgent("WINFUT", 122);   // conhecido
        health.RecordBubbleAgent("WINFUT", 424242); // desconhecido

        var unknown = Assert.Single(health.GetUnknownAgents());
        Assert.Equal(424242, unknown.AgentCode);
        Assert.Equal("bubble", unknown.Source);
        Assert.Equal(1, unknown.Count);
    }

    [Fact]
    public void RecentErrors_RespectsRingBufferAndOrder()
    {
        var health = new DashboardHealthService();

        for (var i = 1; i <= DashboardHealthService.MaxRecentErrors + 5; i++)
            health.RecordError("Warning", "Test", $"erro {i}");

        var errors = health.GetRecentErrors(1000);
        Assert.Equal(DashboardHealthService.MaxRecentErrors, errors.Count);
        Assert.Equal($"erro {DashboardHealthService.MaxRecentErrors + 5}", errors[0].Message);
        Assert.DoesNotContain(errors, e => e.Message == "erro 1");
    }

    [Fact]
    public void StatusForAge_UsesMarketWindowAndThresholds()
    {
        var now = new DateTime(2026, 11, 6, 10, 0, 0); // sexta-feira

        Assert.True(DashboardHealthService.IsMarketOpen(now));
        Assert.False(DashboardHealthService.IsMarketOpen(new DateTime(2026, 11, 7, 10, 0, 0))); // sábado
        Assert.False(DashboardHealthService.IsMarketOpen(new DateTime(2026, 11, 6, 8, 0, 0)));
        Assert.False(DashboardHealthService.IsMarketOpen(new DateTime(2026, 11, 6, 19, 0, 0)));

        Assert.Equal(DashboardStatus.Error,
            DashboardHealthService.StatusForAge(null, now, 60, 300, marketOpen: true));
        Assert.Equal(DashboardStatus.Idle,
            DashboardHealthService.StatusForAge(null, now, 60, 300, marketOpen: false));

        Assert.Equal(DashboardStatus.Ok,
            DashboardHealthService.StatusForAge(now.AddSeconds(-30), now, 60, 300, marketOpen: true));
        Assert.Equal(DashboardStatus.Warn,
            DashboardHealthService.StatusForAge(now.AddSeconds(-120), now, 60, 300, marketOpen: true));
        Assert.Equal(DashboardStatus.Error,
            DashboardHealthService.StatusForAge(now.AddSeconds(-600), now, 60, 300, marketOpen: true));
        Assert.Equal(DashboardStatus.Idle,
            DashboardHealthService.StatusForAge(now.AddSeconds(-600), now, 60, 300, marketOpen: false));
    }

    [Fact]
    public void WorstStatus_PicksMostSevere()
    {
        Assert.Equal(DashboardStatus.Error,
            DashboardHealthService.WorstStatus(DashboardStatus.Ok, DashboardStatus.Warn, DashboardStatus.Error));
        Assert.Equal(DashboardStatus.Warn,
            DashboardHealthService.WorstStatus(DashboardStatus.Ok, DashboardStatus.Warn, DashboardStatus.Idle));
        Assert.Equal(DashboardStatus.Ok,
            DashboardHealthService.WorstStatus(DashboardStatus.Ok, DashboardStatus.Idle));
        Assert.Equal(DashboardStatus.Idle,
            DashboardHealthService.WorstStatus());
    }

    [Fact]
    public void IsKnownAgent_MatchesEnum()
    {
        Assert.True(DashboardHealthService.IsKnownAgent(122));
        Assert.False(DashboardHealthService.IsKnownAgent(999999));
        Assert.Equal("BGC_Liquidez", DashboardHealthService.AgentName(122));
        Assert.Equal("", DashboardHealthService.AgentName(999999));
    }
}
