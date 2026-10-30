using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Tests;

/// <summary>
/// Contrato do prompt: montagem do state JSON e leitura da resposta, sem I/O
/// e sem chave (JevService só transporta).
/// </summary>
public class JevPromptTests
{
    private static readonly DateTime Day = new(2026, 10, 2);

    private static MarketSnapshot Snap()
    {
        var snap = new MarketSnapshot();
        snap.Layers[SnapshotLayers.Header] = new HeaderView
        {
            DisplayDate = Day,
            Mode = "intraday",
            LastPrice = 101.5,
            VisibleTimeFrames = new() { 2 },
            MinDistance = 405,
            ThresholdBubble = 475,
            VwapVisible = true,
            PanelOpen = true,
            BubblesTotalDay = 10,
            BubblesVisibleDay = 2,
        };
        snap.Layers[SnapshotLayers.ScopeIntraday] = new DateWindow
        {
            From = Day.AddHours(10), To = Day.AddHours(10).AddMinutes(2),
        };
        snap.Layers[SnapshotLayers.ScopeDaily] = new DateWindow
        {
            From = Day.AddDays(-5), To = Day,
        };
        snap.Layers[SnapshotLayers.Candles] = new List<BarStorageItem>
        {
            new() { Date = Day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, Open = 100, High = 102, Low = 99, Close = 101, Volume = 10, VwapPrice = 100.5 },
        };
        snap.Layers[SnapshotLayers.Bubbles] = new List<BubbleStorageItem>
        {
            new() { Agent = 122, Amount = 500, Price = 101.5, Date = Day.AddHours(10).AddMinutes(1), ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT" },
        };
        snap.Layers[SnapshotLayers.Volume] = new VolumeLevelStorageItem
        {
            Symbol = "WINFUT", Date = Day,
            Volumes = new() { new VolumeLevel { Price = 101, Total = 100, BuyVolume = 70, SellVolume = 30 } },
        };
        snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
        {
            new() { Date = Day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99, UpAuxBorder = 102, DownAuxBorder = 100 },
            new() { Date = Day.AddDays(-1), Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 110, DownBorder = 90, UpAuxBorder = 108, DownAuxBorder = 92 },
        };
        snap.Layers[SnapshotLayers.ExtremesIntra] = new ExtremeStorageItem
        {
            Symbol = "WINFUT", Date = Day,
            Extremes = new List<ExtremePoint>
            {
                new() { Type = ExtremeType.Top, Position = 103, Value = 1000, Prominence = 500, Confidence = 0.9 },
                new() { Type = ExtremeType.Valley, Position = 99, Value = 900, Prominence = 400, Confidence = 0.8 },
                new() { Type = ExtremeType.Indeterminate, Position = 95, Value = 10, Prominence = 5, Confidence = 0.1 },
            },
        };
        snap.Layers[SnapshotLayers.PivotIntra] = new PivotStorageItem
        {
            Symbol = "WINFUT", Date = Day, Source = "D-1",
            High = 105, Low = 95, Close = 100, LineCount = 2,
            Levels = new List<PivotLevel> { new() { Key = "P", Value = 100 }, new() { Key = "R1", Value = 102 } },
        };
        snap.Layers[SnapshotLayers.DailyCandles] = new List<BarStorageItem>
        {
            new() { Date = Day, Symbol = "WINFUT", TimeFrame = 1440, Open = 98, High = 106, Low = 97, Close = 105, Volume = 5000, VwapPrice = 101 },
        };
        snap.Layers[SnapshotLayers.DailyVolume] = new VolumeLevelStorageItem
        {
            Symbol = "WINFUT", Date = Day,
            Volumes = new() { new VolumeLevel { Price = 100, Total = 500, BuyVolume = 300, SellVolume = 200 } },
        };
        snap.Layers[SnapshotLayers.PivotDaily] = new PivotStorageItem
        {
            Symbol = "WINFUT", Date = Day, Source = "W-1",
            High = 110, Low = 90, Close = 100, LineCount = 1,
            Levels = new List<PivotLevel> { new() { Key = "P", Value = 100 } },
        };
        snap.Layers[SnapshotLayers.ExtremesDaily] = new ExtremeStorageItem
        {
            Symbol = "WINFUT", Date = Day,
            Extremes = new() { new() { Type = ExtremeType.Top, Position = 106, Value = 100, Prominence = 50, Confidence = 0.7 } },
        };
        return snap;
    }

    [Fact]
    public void BuildStateJson_MapsIntradayBlock()
    {
        var s = JevPrompt.BuildStateJson("WINFUT", 2, Snap());

        Assert.Equal("WINFUT", s.Symbol);
        Assert.Equal(2, s.StrategyTimeframe);
        // Scope intraday.
        Assert.Equal(Day.AddHours(10), s.Intraday.Scope.From);
        Assert.Equal(Day.AddHours(10).AddMinutes(2), s.Intraday.Scope.To);
        // Candles.
        var c = Assert.Single(s.Intraday.Candles);
        Assert.Equal(101, c.Close);
        Assert.Equal(10, c.Volume);
        Assert.Equal(100.5, c.Vwap);
        // Bolhas (side traduzido).
        var b = Assert.Single(s.Intraday.Bubbles);
        Assert.Equal("buy", b.Side);
        Assert.Equal("BGC LIQUIDEZ DTVM", b.Agent);
        // Volume profile.
        Assert.NotNull(s.Intraday.VolumeProfile);
        Assert.Equal(101, s.Intraday.VolumeProfile!.Poc);
        Assert.Equal(40, s.Intraday.VolumeProfile.DeltaTotal);
        Assert.Empty(s.Intraday.VolumeProfile.Levels);
        var volumeJson = JsonSerializer.Serialize(s.Intraday.VolumeProfile);
        Assert.Contains("\"poc\":101", volumeJson);
        Assert.Contains("\"delta_total\":40", volumeJson);
        Assert.Contains("\"levels\":[]", volumeJson);
        // Pivots com toque (candle 99..102 contém 100 e 102).
        Assert.Equal(2, s.Intraday.Pivots.Count);
        Assert.All(s.Intraday.Pivots, p => Assert.NotNull(p.TouchedAt));
        // Topos/vales: só Top/Valley (Indeterminate fora), ordenado por preço.
        Assert.Equal(2, s.Intraday.TopsValleys.Count);
        Assert.Equal("vale", s.Intraday.TopsValleys[0].Type);
        Assert.Equal(99, s.Intraday.TopsValleys[0].Price);
        Assert.Equal("topo", s.Intraday.TopsValleys[1].Type);
        Assert.Equal(103, s.Intraday.TopsValleys[1].Price);
        // Structures intraday trazem o timeframe.
        Assert.Contains(s.Intraday.Structures, x => x.Timeframe == 2);
        Assert.DoesNotContain(s.Intraday.Structures, x => x.Timeframe == 1440);
    }

    [Fact]
    public void AgentName_UsesEnumDescription_AndFallsBackForUnknown()
    {
        Assert.Equal("BGC LIQUIDEZ DTVM", JevPrompt.AgentName(122));
        Assert.Equal("GOLDMAN SACHS DO BRASIL CTVM", JevPrompt.AgentName(238));
        Assert.Equal("AGENTE 999999", JevPrompt.AgentName(999999));
    }

    [Fact]
    public void BuildStateJson_AddsNearestPivotLevelContextAndTriggerFlag()
    {
        var at = Day.AddHours(10);
        var triggerBubble = new BubbleStorageItem
        {
            Agent = 238, Amount = 475, Price = 99.0, Date = at,
            ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT",
        };
        var clusterBubble = new BubbleStorageItem
        {
            Agent = 8, Amount = 100, Price = 98.9, Date = at.AddSeconds(30),
            ActionType = Ticks2.ActionType.Sale, Symbol = "WINFUT",
        };
        var snap = new MarketSnapshot();
        snap.Layers[SnapshotLayers.Candles] = new List<BarStorageItem>
        {
            new() { Date = at, Symbol = "WINFUT", TimeFrame = 2, Open = 99.5, High = 100, Low = 98.8, Close = 99.2, Volume = 10, VwapPrice = 99.3 },
        };
        snap.Layers[SnapshotLayers.PivotIntra] = new PivotStorageItem
        {
            Symbol = "WINFUT", Date = Day, Source = "D-1",
            Levels = new() { new() { Key = "S2", Value = 99.0 }, new() { Key = "S1", Value = 102 } },
        };
        snap.Layers[SnapshotLayers.ExtremesIntra] = new ExtremeStorageItem
        {
            Symbol = "WINFUT", Date = Day,
            Extremes = new() { new() { Type = ExtremeType.Valley, Position = 98.9, Value = 1000, Prominence = 500, Confidence = 0.9 } },
        };
        snap.Layers[SnapshotLayers.Bubbles] = new List<BubbleStorageItem> { triggerBubble, clusterBubble };
        snap.Raw["bubblesPool"] = new List<BubbleStorageItem> { clusterBubble, triggerBubble };
        snap.Raw["triggerBubbleKeys"] = new HashSet<string> { JevPrompt.BubbleKey(triggerBubble) };

        var state = JevPrompt.BuildStateJson("WINFUT", 2, snap);
        Assert.Equal("S2", state.Intraday.NearestPivot?.Level);
        Assert.True(state.Intraday.NearestPivot!.DistanceTicks < 1);
        Assert.Contains(state.Intraday.LevelContext,
            l => l.Level == "S2" && l.Bubbles.BuyAmount == 475 && l.Reclaimed);
        var sent = state.Intraday.Bubbles.Single(b => b.Price == 99.0);
        Assert.True(sent.IsTrigger);
        Assert.False(state.Intraday.Bubbles.Single(b => b.Price == 98.9).IsTrigger);

        var cluster = JevPrompt.SelectLevelClusterBubbles(snap, at.AddMinutes(2));
        Assert.Contains(cluster, b => b.Price == 98.9);
    }

    [Fact]
    public void BuildStateJson_IncludeVolumeLevels_True_KeepsLevels()
    {
        var s = JevPrompt.BuildStateJson("WINFUT", 2, Snap(), includeVolumeLevels: true);

        Assert.NotNull(s.Intraday.VolumeProfile);
        Assert.Equal(40, Assert.Single(s.Intraday.VolumeProfile!.Levels).Delta);
    }

    [Fact]
    public void BuildStateJson_DailyBlock_HasNoBubblesNorVwap()
    {
        var s = JevPrompt.BuildStateJson("WINFUT", 2, Snap());
        // Diário: candles + volume + pivots + extremos + structures (sem timeframe).
        Assert.Single(s.Daily.Candles);
        Assert.NotNull(s.Daily.VolumeProfile);
        Assert.Empty(s.Daily.VolumeProfile!.Levels);
        Assert.Equal("W-1", Assert.Single(s.Daily.Pivots).Source);
        Assert.Single(s.Daily.TopsValleys);
        var st = Assert.Single(s.Daily.Structures);
        Assert.Null(st.Timeframe);

        // No JSON serializado, o bloco daily NÃO expõe bubbles/vwap.
        var json = JsonSerializer.Serialize(s);
        using var doc = JsonDocument.Parse(json);
        var daily = doc.RootElement.GetProperty("daily");
        Assert.False(daily.TryGetProperty("bubbles", out _));
        Assert.False(daily.TryGetProperty("vwap", out _));
        Assert.True(daily.TryGetProperty("volume_profile", out _));
        // Chaves snake_case do contrato, não PascalCase.
        Assert.True(doc.RootElement.TryGetProperty("strategy_timeframe", out _));
    }

    [Fact]
    public void BuildStateJson_StructureTimeframeOmittedInDaily()
    {
        var s = JevPrompt.BuildStateJson("WINFUT", 2, Snap());
        var json = JsonSerializer.Serialize(s.Daily.Structures);
        Assert.DoesNotContain("timeframe", json);
        Assert.Contains("up_aux", json);
    }

    [Fact]
    public void BuildStateJson_IntradayStructures_OnlyStrategyTfInsideScope()
    {
        var snap = Snap();
        snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
        {
            new() { Date = Day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99 },
            new() { Date = Day.AddHours(10).AddMinutes(1), Symbol = "WINFUT", TimeFrame = 5, UpBorder = 104, DownBorder = 98 },
            new() { Date = Day.AddHours(9), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 105, DownBorder = 97 },
            new() { Date = Day.AddDays(-1), Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 110, DownBorder = 90 },
        };

        var s = JevPrompt.BuildStateJson("WINFUT", 2, snap);

        var st = Assert.Single(s.Intraday.Structures);
        Assert.Equal(103, st.Up);
        Assert.Equal(2, st.Timeframe);
    }

    [Fact]
    public void BuildStateJson_IntradayStructures_NoScope_DoesNotFallbackToHistory()
    {
        var snap = Snap();
        snap.Layers.Remove(SnapshotLayers.ScopeIntraday);
        snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
        {
            new() { Date = Day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99 },
            new() { Date = Day.AddDays(-5), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 100, DownBorder = 90 },
        };

        var s = JevPrompt.BuildStateJson("WINFUT", 2, snap);

        Assert.Empty(s.Intraday.Structures);
    }

    [Fact]
    public void BuildStateJson_IntradayStructures_DeduplicatesByDate()
    {
        var snap = Snap();
        snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
        {
            new() { Date = Day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99 },
            new() { Date = Day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 104, DownBorder = 98 },
        };

        var s = JevPrompt.BuildStateJson("WINFUT", 2, snap);

        var st = Assert.Single(s.Intraday.Structures);
        Assert.Equal(104, st.Up);
    }

    [Fact]
    public void BuildStateJson_DailyStructures_Only1440InsideScopeOnePerDay()
    {
        var snap = Snap();
        snap.Layers[SnapshotLayers.ScopeDaily] = new DateWindow
        {
            From = Day.AddDays(-2), To = Day,
        };
        snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
        {
            new() { Date = Day.AddDays(-1), Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 110, DownBorder = 90 },
            new() { Date = Day.AddDays(-1).AddHours(12), Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 111, DownBorder = 91 },
            new() { Date = Day.AddDays(-3), Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 120, DownBorder = 80 },
            new() { Date = Day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99 },
        };

        var s = JevPrompt.BuildStateJson("WINFUT", 2, snap);

        var st = Assert.Single(s.Daily.Structures);
        Assert.Equal(111, st.Up);
        Assert.Null(st.Timeframe);
    }

    [Fact]
    public void ApplyBubbleFilter_MatchesAppPredicate()
    {
        BubbleStorageItem B(int agent, int amount) => new()
        {
            Agent = agent, Amount = amount, Price = 101.5, Date = DateTime.Now,
            ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT",
        };
        var all = new List<BubbleStorageItem>
        {
            B(122, 500),   // ok: selecionado, >= 475
            B(122, 474),   // fora: abaixo do base
            B(114, 900),   // fora: agente com threshold mas desmarcado
            B(3, 383),     // fora: 383 < 1225 (caso real do 09:04!)
            B(120, 900),   // fora: agente desmarcado
        };
        // Valores reais do export (b3wm_config.json, WINFUT).
        var vis = JevPrompt.ApplyBubbleFilter(all, 475,
            new Dictionary<int, int> { [114] = 700, [3] = 1225, [39] = 1000, [6003] = 800 },
            new HashSet<int> { 238, 16, 13, 122, 127, 77, 40 }, true, true);
        var only = Assert.Single(vis);
        Assert.Equal(122, only.Agent);

        // Flags off = tudo passa (igual aos toggles da aba).
        var open = JevPrompt.ApplyBubbleFilter(all, 475,
            new Dictionary<int, int>(), new HashSet<int>(), false, false);
        Assert.Equal(all.Count, open.Count);
    }

    [Fact]
    public void BuildQuestions_HasPairedDirectionIndicators_NoEncerrar()
    {
        var q = JevPrompt.BuildQuestions();
        // 10 dimensões (20 perguntas alta/baixa) + 5 de operabilidade.
        Assert.Equal(25, q.Count);
        Assert.Equal(10, JevPrompt.DirectionDimensions.Length);
        foreach (var dim in JevPrompt.DirectionDimensions)
        {
            Assert.True(q.ContainsKey(dim.BullId), $"pergunta de alta ausente: {dim.BullId}");
            Assert.True(q.ContainsKey(dim.BearId), $"pergunta de baixa ausente: {dim.BearId}");
        }
        foreach (var id in JevPrompt.OperabilityIndicators)
            Assert.True(q.ContainsKey(id), $"indicador de operabilidade ausente: {id}");
        // Não existe mais pergunta de direção/gate/encerrar.
        Assert.False(q.ContainsKey("direcao"));
        Assert.False(q.ContainsKey("confianca_operavel"));
        Assert.False(q.ContainsKey("encerrar_posicao"));
    }

    private const string AnswersComprar = """
        {"model":"jev-1.13.0","answers":{
        "candles_alta":{"type":"noul","noul":0.90},
        "candles_baixa":{"type":"noul","noul":0.10},
        "acima_vwap":{"type":"noul","noul":0.80},
        "abaixo_vwap":{"type":"noul","noul":0.15},
        "estrutura_alta":{"type":"noul","noul":0.70},
        "estrutura_baixa":{"type":"noul","noul":0.20},
        "acima_poc":{"type":"noul","noul":0.60},
        "abaixo_poc":{"type":"noul","noul":0.25},
        "delta_positivo":{"type":"noul","noul":0.65},
        "delta_negativo":{"type":"noul","noul":0.20},
        "bolhas_compra":{"type":"noul","noul":0.55},
        "bolhas_venda":{"type":"noul","noul":0.20},
        "defende_suporte":{"type":"noul","noul":0.75},
        "rejeita_resistencia":{"type":"noul","noul":0.20},
        "reversao_suporte":{"type":"noul","noul":0.80},
        "reversao_resistencia":{"type":"noul","noul":0.20},
        "exaustao_queda":{"type":"noul","noul":0.70},
        "exaustao_alta":{"type":"noul","noul":0.20},
        "diario_alta":{"type":"noul","noul":0.85},
        "diario_baixa":{"type":"noul","noul":0.10},
        "sem_chop":{"type":"noul","noul":0.70},
        "amplitude_suficiente":{"type":"noul","noul":0.80},
        "sem_compressao_topovale":{"type":"noul","noul":0.60},
        "referencia_clara":{"type":"noul","noul":0.90},
        "sem_conflito_intra_diario":{"type":"noul","noul":0.75}},
        "usage":{"input_tokens":100,"output_tokens":10}}
        """;

    private const string AnswersLateral = """
        {"answers":{
        "candles_alta":{"type":"noul","noul":0.80},
        "candles_baixa":{"type":"noul","noul":0.80},
        "acima_vwap":{"type":"noul","noul":0.80},
        "abaixo_vwap":{"type":"noul","noul":0.80},
        "estrutura_alta":{"type":"noul","noul":0.80},
        "estrutura_baixa":{"type":"noul","noul":0.80},
        "acima_poc":{"type":"noul","noul":0.80},
        "abaixo_poc":{"type":"noul","noul":0.80},
        "delta_positivo":{"type":"noul","noul":0.80},
        "delta_negativo":{"type":"noul","noul":0.80},
        "bolhas_compra":{"type":"noul","noul":0.80},
        "bolhas_venda":{"type":"noul","noul":0.80},
        "defende_suporte":{"type":"noul","noul":0.80},
        "rejeita_resistencia":{"type":"noul","noul":0.80},
        "reversao_suporte":{"type":"noul","noul":0.80},
        "reversao_resistencia":{"type":"noul","noul":0.80},
        "exaustao_queda":{"type":"noul","noul":0.80},
        "exaustao_alta":{"type":"noul","noul":0.80},
        "diario_alta":{"type":"noul","noul":0.80},
        "diario_baixa":{"type":"noul","noul":0.80},
        "sem_chop":{"type":"noul","noul":0.50},
        "amplitude_suficiente":{"type":"noul","noul":0.50},
        "sem_compressao_topovale":{"type":"noul","noul":0.50},
        "referencia_clara":{"type":"noul","noul":0.50},
        "sem_conflito_intra_diario":{"type":"noul","noul":0.50}}}
        """;

    private const string StateJson = """
        {"intraday":{"candles":[{"close":100.0}],"vwap":[{"vwap":99.0}],
        "pivots":[],"structures":[],"tops_valleys":[]},"daily":{}}
        """;

    [Fact]
    public void ParseDecision_AggregatesCriteriaAndProximity()
    {
        var r = JevPrompt.ParseDecision("WINFUT", "jev-1.13.0", AnswersComprar, 100, 10, StateJson);

        // Evidência de alta com baixa fraca nos dois lados de cada dimensão.
        Assert.Equal("comprar", r.Direcao);
        Assert.True(r.DirecaoConfidence > 0);
        Assert.True(r.ConfiancaOperavel > 0);
        Assert.True(r.DirecaoNet > 0);
        Assert.Equal(1, r.DirecaoCobertura, 6);
        Assert.False(string.IsNullOrWhiteSpace(r.DirecaoMotivo));
        Assert.Contains("alta", r.DirecaoMotivo);
        Assert.Equal(25, r.Criteria.Count);
        Assert.Equal(0.90, r.Criteria["candles_alta"]);
        Assert.Equal(0.10, r.Criteria["candles_baixa"]);
        Assert.Equal(100, r.InputTokens);
        Assert.Equal(10, r.OutputTokens);

        // Proximidade: preço 100, VWAP 99, tick WINFUT 5 → 1 tick da vwap.
        Assert.Equal("vwap", r.ProximidadeRef);
        Assert.True(r.Proximidade > 0.9);
    }

    [Fact]
    public void ParseDecision_ConflictingSides_DefaultLateral()
    {
        var r = JevPrompt.ParseDecision("WINFUT", "m", AnswersLateral, 0, 0, StateJson);
        Assert.Equal("lateral", r.Direcao);
        Assert.Equal(0, r.DirecaoConfidence);
        Assert.Equal(0, r.DirecaoNet, 6);
        Assert.Equal("evidência fraca", r.DirecaoMotivo);
    }

    [Fact]
    public void ParseDecision_MissingFields_DefaultLateral()
    {
        var r = JevPrompt.ParseDecision("WINFUT", "m", """{"answers":{}}""", 0, 0, "{}");
        Assert.Equal("lateral", r.Direcao);
        Assert.Equal(0, r.DirecaoConfidence);
        Assert.Equal("sem dados", r.DirecaoMotivo);
        Assert.Empty(r.Criteria);
        Assert.Equal(0, r.Proximidade);
    }
}
