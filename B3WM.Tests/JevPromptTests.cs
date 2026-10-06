using B3WM.Services.AI;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Tests;

/// <summary>
/// Contrato do prompt: montagem do state e leitura da resposta, sem I/O
/// e sem chave (JevService só transporta).
/// </summary>
public class JevPromptTests
{
    private static MarketSnapshot Snap(
        List<BarStorageItem>? candles = null,
        List<BubbleStorageItem>? bubbles = null,
        VolumeLevelStorageItem? volume = null,
        PositionView? position = null,
        bool structuresOff = false,
        bool extremesOff = false,
        bool pivotOff = false,
        double? vwap = null)
    {
        var snap = new MarketSnapshot();
        snap.Layers[SnapshotLayers.Header] = new HeaderView
        {
            DisplayDate = new DateTime(2026, 10, 2),
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
        snap.Layers[SnapshotLayers.Candles] = candles ?? new();
        if (bubbles != null) snap.Layers[SnapshotLayers.Bubbles] = bubbles;
        if (volume != null) snap.Layers[SnapshotLayers.Volume] = volume;
        if (position != null) snap.Layers[SnapshotLayers.Position] = position;
        if (!extremesOff)
            snap.Layers[SnapshotLayers.ExtremesIntra] = new ExtremeStorageItem
            {
                Symbol = "WINFUT",
                Date = new DateTime(2026, 10, 2),
                Extremes = new List<ExtremePoint>
                {
                    new() { Type = ExtremeType.Top, Position = 103, Value = 1000, Prominence = 500, Confidence = 0.9 },
                    new() { Type = ExtremeType.Valley, Position = 99, Value = 900, Prominence = 400, Confidence = 0.8 },
                    new() { Type = ExtremeType.Indeterminate, Position = 95, Value = 10, Prominence = 5, Confidence = 0.1 },
                },
            };
        if (!structuresOff)
            snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
            {
                new() { Date = new DateTime(2026, 10, 2, 10, 0, 0), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99, UpAuxBorder = 102, DownAuxBorder = 100 },
            };
        if (!pivotOff)
            snap.Layers[SnapshotLayers.PivotIntra] = new PivotStorageItem
            {
                Symbol = "WINFUT", Date = new DateTime(2026, 10, 2), Source = "D-1",
                High = 105, Low = 95, Close = 100, LineCount = 2,
                Levels = new List<PivotLevel> { new() { Key = "P", Value = 100 }, new() { Key = "R1", Value = 102 } },
            };
        if (vwap != null) snap.Layers[SnapshotLayers.Vwap] = vwap;
        return snap;
    }

    private static BarStorageItem Bar(DateTime date, double o, double h, double l, double c, long v) => new()
    {
        Date = date, Symbol = "WINFUT", TimeFrame = 2,
        Open = o, High = h, Low = l, Close = c, Volume = v,
    };

    private static BubbleStorageItem Bubble(int agent, decimal amount, DateTime date) => new()
    {
        Agent = agent, Amount = amount, Price = 101.5, Date = date,
        ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT",
    };

    [Fact]
    public void BuildState_ContainsAllLayers()
    {
        var day = new DateTime(2026, 10, 2);
        var state = JevPrompt.BuildState("WINFUT", 2, Snap(
            candles: new() { Bar(day.AddHours(10), 100, 102, 99, 101, 10) },
            bubbles: new() { Bubble(122, 500, day.AddHours(10).AddMinutes(1)) },
            volume: new VolumeLevelStorageItem
            {
                Symbol = "WINFUT", Date = day,
                Volumes = new() { new VolumeLevel { Price = 101, Total = 100, BuyVolume = 70, SellVolume = 30 } },
            },
            vwap: 100.0));

        Assert.Contains("Ativo: WINFUT Timeframe: 2min", state);
        Assert.Contains("Tela: dia=2026-10-02", state);
        Assert.Contains("O=100 H=102 L=99 C=101", state);
        Assert.Contains("agente=122", state);
        Assert.Contains("POC=101", state);
        Assert.Contains("Posição: flat", state);
        Assert.Contains("Topos e vales intraday (1 topos, 1 vales, 1 indet.", state);
        Assert.Contains("topo preco=103", state);
        Assert.DoesNotContain("Indeterminate", state);
        Assert.Contains("[tf 2] resistencia preco=103", state);
        Assert.Contains("Pivot intraday (D-1", state);
        Assert.Contains("P=100 R1=102", state);
        Assert.Contains("VWAP do dia: vwap=100.0 preço acima (+1.48%)", state);
    }

    [Fact]
    public void BuildState_HiddenLayers_SayDesligado()
    {
        var state = JevPrompt.BuildState("WINFUT", 2, Snap(
            structuresOff: true, extremesOff: true, pivotOff: true));
        Assert.Contains("Bolhas: desligadas na tela.", state);
        Assert.Contains("Volume profile: desligado na tela.", state);
        Assert.Contains("Structures: desligadas na tela.", state);
        Assert.Contains("Topos e vales intraday: desligados na tela.", state);
        Assert.Contains("Pivot intraday: desligado na tela.", state);
        Assert.Contains("VWAP: off ou sem dados.", state);
        Assert.Contains("Posição: flat", state);
    }

    [Fact]
    public void BuildState_PositionBlock_Open()
    {
        var state = JevPrompt.BuildState("WINFUT", 2, Snap(position: new PositionView
        {
            Side = "comprar", Entry = 188000,
            EntryTime = new DateTime(2026, 10, 2, 10, 4, 0),
            UnrealizedPts = 120, Candles = 6,
        }));
        Assert.Contains("Posição: comprado @188000 desde 10:04 (6 candles, parcial +120 pts).", state);
    }

    [Fact]
    public void ApplyBubbleFilter_MatchesAppPredicate()
    {
        BubbleStorageItem B(int agent, int amount) => Bubble(agent, amount, DateTime.Now);
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
    public void ComputeVwap_MatchesChartFormula()
    {
        var bars = new List<BarStorageItem>
        {
            Bar(new DateTime(2026, 10, 2, 10, 0, 0), 0, 102, 98, 100, 10),
            Bar(new DateTime(2026, 10, 2, 10, 5, 0), 0, 104, 100, 103, 30),
            Bar(new DateTime(2026, 10, 1, 10, 0, 0), 0, 200, 200, 200, 999), // outro dia: fora
        };
        var expected = (100.0 * 10 + (104.0 + 100.0 + 103.0) / 3 * 30) / 40;
        Assert.Equal(expected, JevPrompt.ComputeVwap(bars, new DateTime(2026, 10, 2))!.Value, precision: 6);
        Assert.Null(JevPrompt.ComputeVwap(new List<BarStorageItem>(), new DateTime(2026, 10, 2)));
    }

    [Fact]
    public void BuildQuestions_HasFourQuesitos()
    {
        var q = (System.Collections.Generic.Dictionary<string, object>)JevPrompt.BuildQuestions();
        Assert.True(q.ContainsKey("direcao"));
        Assert.True(q.ContainsKey("forca_tendencia"));
        Assert.True(q.ContainsKey("confianca_operavel"));
        Assert.True(q.ContainsKey("encerrar_posicao"));
    }

    private const string AnswersComprar = """
        {"model":"jev-1.13.0","answers":{
        "direcao":{"type":"choice","choice":"comprar","confidence":0.85,"probabilities":{"comprar":0.8,"vender":0.05,"manter":0.15}},
        "forca_tendencia":{"type":"score","score":1.0,"confidence":0.7},
        "confianca_operavel":{"type":"noul","noul":0.9},
        "encerrar_posicao":{"type":"noul","noul":0.1}},
        "usage":{"input_tokens":100,"output_tokens":10}}
        """;

    [Fact]
    public void ParseDecision_MapsGateAndTokens()
    {
        var r = JevPrompt.ParseDecision("WINFUT", "jev-1.13.0", AnswersComprar, 100, 10, 0.7, "state-text");
        Assert.Equal("comprar", r.Direcao);
        Assert.Equal(0.85, r.DirecaoConfidence);
        Assert.Equal(0.8, r.DirecaoProbabilities["comprar"]);
        Assert.Equal(1.0, r.ForcaTendencia);
        Assert.Equal(0.9, r.ConfiancaOperavel);
        Assert.Equal(0.1, r.EncerrarPosicao);
        Assert.True(r.ShouldTrade);
        Assert.Equal(100, r.InputTokens);
        Assert.Equal(10, r.OutputTokens);

        // Gate barra manter e confiança baixa (mesmo corpo, threshold alto).
        var r2 = JevPrompt.ParseDecision("WINFUT", "m", AnswersComprar, 0, 0, 0.95, "s");
        Assert.False(r2.ShouldTrade);
    }

    [Fact]
    public void ParseDecision_MissingFields_DefaultManter()
    {
        var r = JevPrompt.ParseDecision("WINFUT", "m", """{"answers":{}}""", 0, 0, 0.7, "s");
        Assert.Equal("manter", r.Direcao);
        Assert.False(r.ShouldTrade);
        Assert.Equal(0, r.EncerrarPosicao);
    }
}
