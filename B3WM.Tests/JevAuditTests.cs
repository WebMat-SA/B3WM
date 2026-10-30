using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Tests;

/// <summary>
/// Auditoria Jev (issue #23): 1 arquivo por chamada em Data/JEV,
/// com o que chegou, filtrou, perguntou, chamou e respondeu.
/// </summary>
public class JevAuditTests
{
    private static MarketSnapshot Snap()
    {
        var snap = new MarketSnapshot();
        var day = new DateTime(2026, 10, 2);
        snap.Layers[SnapshotLayers.Header] = new HeaderView
        {
            DisplayDate = day,
            Mode = "intraday",
            LastPrice = 101.5,
            VisibleTimeFrames = new() { 2 },
            MinDistance = 405,
            ThresholdBubble = 475,
            VwapVisible = true,
            PanelOpen = false,
            BubblesTotalDay = 10,
            BubblesVisibleDay = 2,
        };
        snap.Layers[SnapshotLayers.Candles] = new List<BarStorageItem>
        {
            new() { Date = day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, Open = 100, High = 102, Low = 99, Close = 101, Volume = 10 },
        };
        var pool = new List<BubbleStorageItem>
        {
            new() { Agent = 122, Amount = 500, Price = 101.5, Date = day.AddHours(10).AddMinutes(1), ActionType = Ticks2.ActionType.Buy, Symbol = "WINFUT" },
            new() { Agent = 122, Amount = 100, Price = 101.0, Date = day.AddHours(10).AddMinutes(2), ActionType = Ticks2.ActionType.Sale, Symbol = "WINFUT" },
        };
        snap.Layers[SnapshotLayers.Bubbles] = new List<BubbleStorageItem> { pool[0] };
        // Auditoria: pool cru (2) + pós-filtro de tela (1) + spec cru.
        snap.Raw["bubblesPool"] = pool;
        snap.Raw["bubblesFiltered"] = new List<BubbleStorageItem> { pool[0] };
        snap.Raw["spec"] = System.Text.Json.JsonSerializer.SerializeToElement(new { thresholdBubble = 475 });
        snap.Layers[SnapshotLayers.Volume] = new VolumeLevelStorageItem
        {
            Symbol = "WINFUT", Date = day,
            Volumes = new() { new VolumeLevel { Price = 101, Total = 100, BuyVolume = 70, SellVolume = 30 } },
        };
        snap.Layers[SnapshotLayers.ExtremesIntra] = new ExtremeStorageItem
        {
            Symbol = "WINFUT", Date = day,
            Extremes = new() { new() { Type = ExtremeType.Top, Position = 103, Value = 1000, Prominence = 500, Confidence = 0.9 } },
        };
        snap.Layers[SnapshotLayers.Position] = new PositionView
        {
            Side = "comprar", Entry = 100, EntryTime = day.AddHours(10),
            UnrealizedPts = 1.5, Candles = 1,
        };
        snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
        {
            new() { Date = day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99 },
        };
        snap.Layers[SnapshotLayers.Vwap] = (double)100.0;
        return snap;
    }

    private static JevAnalysisResult Parsed() => new()
    {
        Symbol = "WINFUT", Model = "jev-1.13.0", Direcao = "comprar",
        DirecaoConfidence = 0.85, DirecaoNet = 0.72,
        DirecaoConsenso = 0.9, DirecaoCobertura = 1.0,
        ConfiancaOperavel = 0.9,
        Criteria = new() { ["candles_alta"] = 0.8, ["sem_chop"] = 0.7 },
        Proximidade = 0.6, ProximidadeTicks = 12, ProximidadeRef = "vwap",
        InputTokens = 100, OutputTokens = 10,
    };

    [Fact]
    public void MeasureBlocks_CoversAllJsonBlocks()
    {
        var snap = Snap();
        var blocks = JevPrompt.MeasureBlocks("WINFUT", 2, snap);
        foreach (var k in new[]
                 {
                     "intraday.candles", "intraday.bubbles", "intraday.volume",
                     "intraday.pivots", "intraday.extremes", "intraday.structures",
                     "daily.candles", "daily.volume", "daily.pivots", "daily.extremes", "daily.structures",
                 })
            Assert.True(blocks.ContainsKey(k), $"bloco {k} ausente");
        var state = System.Text.Json.JsonSerializer.Serialize(JevPrompt.BuildStateJson("WINFUT", 2, snap));
        Assert.Contains("\"agent\":\"BGC LIQUIDEZ DTVM\"", state);
    }

    [Fact]
    public void BuildAudit_MapsChegouFiltrouPerguntaChamadaResposta()
    {
        var snap = Snap();
        var state = System.Text.Json.JsonSerializer.Serialize(JevPrompt.BuildStateJson("WINFUT", 2, snap));
        var q = System.Text.Json.JsonSerializer.Serialize(JevPrompt.BuildQuestions());
        var payload = "{\"state\":" + state + ",\"model\":\"jev-1.13.0\",\"questions\":" + q + "}";
        var r = JevPrompt.BuildAudit("a3f9c1d2",
            new DateTime(2026, 10, 16, 10, 4, 31, 120), new DateTime(2026, 10, 16, 10, 4, 0),
            "BubbleAppeared", "WINFUT", 2, new DateTime(2026, 10, 16),
            "strategy", "jev-1.13.0", "snap123", 60, 30,
            snap, state, payload, 200, 321, 1, """{"answers":{}}""", Parsed());

        Assert.Equal("WINFUT", r.Symbol);
        Assert.Equal("strategy", r.Origin);
        // Chegou: pool do dia + filtro de tela vindos do Header.
        Assert.Equal(10, r.Input.BubblesPoolTotal);
        Assert.Equal(2, r.Input.BubblesAfterScreenFilter);
        Assert.Equal(475, r.Input.ThresholdBubble);
        // Filtrou/enviou: o que foi para o state.
        Assert.Equal(1, r.Filtered.CandlesSent);
        Assert.Equal(1, r.Filtered.BubblesSent);
        Assert.Equal(state.Length, r.Filtered.StateChars);
        // Payload exato enviado: state como objeto + model + questions.
        Assert.Equal(payload, r.Payload);
        Assert.StartsWith("{\"state\":{", r.Payload);
        Assert.Contains("\"questions\":", r.Payload);
        Assert.Contains("candles_alta", r.Payload);
        // Inputs detalhados: JSONs reais de tudo que chegou.
        Assert.NotNull(r.Input.Candles);
        var candlesJson = r.Input.Candles!.Value.EnumerateArray().ToList();
        Assert.Single(candlesJson);
        Assert.Equal(101, candlesJson[0].GetProperty("Close").GetDouble());
        Assert.NotNull(r.Input.BubblesPool);
        Assert.Equal(2, r.Input.BubblesPool!.Value.GetArrayLength());
        Assert.Equal(1, r.Input.BubblesFiltered!.Value.GetArrayLength());
        Assert.Equal(1, r.Input.BubblesSent!.Value.GetArrayLength());
        Assert.Equal("BGC LIQUIDEZ DTVM",
            r.Input.BubblesSent!.Value[0].GetProperty("agent").GetString());
        Assert.NotNull(r.Input.Volume);
        Assert.Equal(1, r.Input.Volume!.Value.GetProperty("Volumes").GetArrayLength());
        Assert.NotNull(r.Input.ExtremesIntra);
        Assert.NotNull(r.Input.Structures);
        Assert.NotNull(r.Input.Header);
        // O state não envia mais posição.
        Assert.Null(r.Input.Position);
        Assert.NotNull(r.Input.ScreenSpec);
        Assert.Equal(475, r.Input.ScreenSpec!.Value.GetProperty("thresholdBubble").GetInt32());
        Assert.Equal(100.0, r.Input.Vwap);
        // Chamada + resposta.
        Assert.Equal(200, r.Call.HttpStatus);
        Assert.Equal(321, r.Call.LatencyMs);
        Assert.Equal("comprar", r.Answer.Direcao);
        Assert.Equal(0.72, r.Answer.DirecaoNet, 6);
        Assert.Equal(0.9, r.Answer.DirecaoConsenso, 6);
        Assert.Equal(100, r.Answer.InputTokens);
    }

    [Fact]
    public void BuildAudit_FilteredStructuresCount_MatchesState_RawStaysInInput()
    {
        var day = new DateTime(2026, 10, 2);
        var snap = Snap();
        snap.Layers[SnapshotLayers.ScopeIntraday] = new DateWindow
        {
            From = day.AddHours(10), To = day.AddHours(10),
        };
        snap.Layers[SnapshotLayers.ScopeDaily] = new DateWindow
        {
            From = day.AddDays(-2), To = day,
        };
        snap.Layers[SnapshotLayers.Structures] = new List<StructureStorageItem>
        {
            new() { Date = day.AddHours(10), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 103, DownBorder = 99 },
            new() { Date = day.AddHours(9), Symbol = "WINFUT", TimeFrame = 2, UpBorder = 104, DownBorder = 98 },
            new() { Date = day.AddHours(10), Symbol = "WINFUT", TimeFrame = 5, UpBorder = 105, DownBorder = 97 },
            new() { Date = day.AddDays(-1), Symbol = "WINFUT", TimeFrame = 1440, UpBorder = 110, DownBorder = 90 },
        };
        var state = System.Text.Json.JsonSerializer.Serialize(
            JevPrompt.BuildStateJson("WINFUT", 2, snap));
        var q = System.Text.Json.JsonSerializer.Serialize(JevPrompt.BuildQuestions());
        var payload = "{\"state\":" + state + ",\"model\":\"jev-1.13.0\",\"questions\":" + q + "}";
        var r = JevPrompt.BuildAudit("audit123",
            new DateTime(2026, 10, 16, 10, 4, 31, 120), new DateTime(2026, 10, 16, 10, 4, 0),
            "BubbleAppeared", "WINFUT", 2, new DateTime(2026, 10, 16),
            "strategy", "jev-1.13.0", "snap123", 60, 30,
            snap, state, payload, 200, 100, 1, """{"answers":{}}""", Parsed());

        // Enviado: 1 intraday (TF 2, dentro do escopo) + 1 diário.
        Assert.Equal(2, r.Filtered.StructuresCount);
        Assert.Equal(payload, r.Payload);
        // Input preserva o cru que chegou para diagnóstico.
        Assert.Equal(4, r.Input.Structures!.Value.GetArrayLength());
    }

    [Fact]
    public void BuildAudit_OnHttpError_RecordsErrorAndState()
    {
        const string body = """{"detail":{"error_type":"max_tokens_exceeded"}}""";
        var snap = Snap();
        var state = System.Text.Json.JsonSerializer.Serialize(
            JevPrompt.BuildStateJson("WINFUT", 2, snap));
        var q = System.Text.Json.JsonSerializer.Serialize(JevPrompt.BuildQuestions());
        var payload = "{\"state\":" + state + ",\"model\":\"jev-1.13.0\",\"questions\":" + q + "}";
        var r = JevPrompt.BuildAudit("err12345",
            new DateTime(2026, 10, 16, 10, 4, 31, 120), new DateTime(2026, 10, 16, 10, 4, 0),
            "BubbleAppeared", "WINFUT", 2, new DateTime(2026, 10, 16),
            "strategy", "jev-1.13.0", "snap123", 60, 30,
            snap, state, payload, 400, 1234, 2, body, null,
            JevPrompt.ParseErrorType(body), "Jev HTTP 400: max_tokens_exceeded");

        Assert.Equal(400, r.Call.HttpStatus);
        Assert.Equal(1234, r.Call.LatencyMs);
        Assert.Equal(2, r.Call.Attempts);
        Assert.Equal("max_tokens_exceeded", r.Call.ErrorType);
        Assert.Equal("Jev HTTP 400: max_tokens_exceeded", r.Call.Error);
        // Corpo cru do erro preservado; resposta interpretada fica default.
        Assert.Equal(body, r.Answer.BodyRaw);
        Assert.Equal("lateral", r.Answer.Direcao);
        Assert.Empty(r.Answer.Criteria);
        // O payload que estourou fica registrado integralmente para diagnóstico.
        Assert.Equal(payload, r.Payload);
        Assert.Contains("candles_alta", r.Payload);
        Assert.Equal(1, r.Filtered.CandlesSent);
    }

    [Theory]
    [InlineData("""{"detail":{"error_type":"max_tokens_exceeded"}}""", "max_tokens_exceeded")]
    [InlineData("""{"detail":{"error_type":"other"}}""", "other")]
    [InlineData("""{"answers":{}}""", "")]
    [InlineData("", "")]
    [InlineData("not json", "")]
    public void ParseErrorType_ExtractsOrEmpty(string body, string expected) =>
        Assert.Equal(expected, JevPrompt.ParseErrorType(body));

    [Fact]
    public void AuditFileName_FormatAndUniqueness()
    {
        var at = new DateTime(2026, 10, 16, 10, 4, 31, 120);
        var f = JevAuditFiles.BuildFileName("winfut", at, "a3f9c1d2");
        Assert.Equal("WINFUT_Jev_2026-10-16_10-04-31-120_a3f9c1d2.json", f);
        Assert.StartsWith("JEV/", JevAuditFiles.BuildPath(f));
        Assert.NotEqual(JevAuditFiles.NewId(), JevAuditFiles.NewId());
    }

    private sealed class TempKeeper : DataKeeperBase
    {
        private readonly string _root;
        public TempKeeper(string root) : base(null) => _root = root;
        protected override string RootDirectory => _root;
    }

    [Fact]
    public async Task DataKeeper_CreatesJevSubfolder_OneFilePerCall()
    {
        var root = Path.Combine(Path.GetTempPath(), "b3wm-jev-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var keeper = new TempKeeper(root);
            var rec = new JevAuditRecord { Id = "abc12345", Symbol = "WINFUT", At = new DateTime(2026, 10, 16, 10, 0, 0) };
            var file = JevAuditFiles.BuildFileName(rec.Symbol, rec.At, rec.Id);
            await keeper.WriteDataAsync(JevAuditFiles.BuildPath(file), rec);
            Assert.True(File.Exists(Path.Combine(root, "JEV", file)));
            var back = await keeper.ReadDataAsync<JevAuditRecord>(JevAuditFiles.BuildPath(file));
            Assert.Equal("abc12345", back.Id);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
