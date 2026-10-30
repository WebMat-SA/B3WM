using System.Text.Json;
using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Services.Screen;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace B3WM.Tests;

/// <summary>
/// Estudo offline da issue #42. Lê Data/JEV somente para leitura, rotula
/// reversões em pivô/vale com os candles futuros do dia e mede quantas a Jev
/// chamou de venda. Opcionalmente reenvia uma amostra com o prompt atual.
///
/// Roda só com B3WM_STUDY_REVERSAL=1:
///   B3WM_STUDY_REVERSAL=1 dotnet test B3WM.Tests -c Release --filter JevReversalStudyTool
/// Para a amostra real, configure TYPESAFE_API_KEY/JevKey e:
///   B3WM_STUDY_REVERSAL=1 B3WM_STUDY_SAMPLE=1 dotnet test ...
/// </summary>
public class JevReversalStudyTool
{
    private const int Horizon = 10;
    private const double BarrierTicks = 10;
    private const double LevelTicks = 30;

    [Fact]
    public async Task StudyReversals_WriteReport()
    {
        if (Environment.GetEnvironmentVariable("B3WM_STUDY_REVERSAL") != "1")
            return;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var dataRoot = Path.Combine(repoRoot, "B3WM", "Data");
        var jevDir = Path.Combine(dataRoot, JevAuditFiles.Dir);
        Assert.True(Directory.Exists(jevDir), "Pasta JEV não encontrada: " + jevDir);

        var keeper = new Keeper(dataRoot);
        var files = Directory.GetFiles(jevDir, "*.json").OrderBy(f => f).ToList();
        var cases = new List<ReversalCase>();
        var dayCache = new Dictionary<string, List<BarStorageItem>>();

        foreach (var file in files)
        {
            JevAuditRecord? rec;
            try { rec = JsonSerializer.Deserialize<JevAuditRecord>(await File.ReadAllTextAsync(file)); }
            catch { continue; }
            if (rec == null || rec.Input.Candles is null) continue;

            var sent = Read<List<BarStorageItem>>(rec.Input.Candles);
            if (sent is not { Count: > 0 }) continue;
            var last = sent[^1];
            var tick = Defaults.GetTickSize(rec.Symbol);
            if (tick <= 0) continue;

            var key = $"{rec.Symbol}_{rec.TimeFrame}_{rec.DisplayDate:yyyy-MM-dd}";
            if (!dayCache.TryGetValue(key, out var day))
            {
                day = await keeper.ReadDataAsync<List<BarStorageItem>>(
                    $"{rec.Symbol}_{nameof(CandleService)}_{rec.TimeFrame}MIN_{rec.DisplayDate:yyyy-MM-dd}.json") ?? new();
                day = day.OrderBy(b => b.Date).ToList();
                dayCache[key] = day;
            }
            if (day.Count == 0) continue;

            var triggerAt = rec.TriggerAt == default ? last.Date.AddMinutes(rec.TimeFrame) : rec.TriggerAt;
            var entryIdx = LastIndexAtOrBefore(day, triggerAt);
            if (entryIdx < 0) continue;

            var support = NearestSupport(rec, sent, tick);
            if (support is null) continue;

            var pool = Read<List<BubbleStorageItem>>(rec.Input.BubblesPool)
                ?? Read<List<BubbleStorageItem>>(rec.Input.BubblesFiltered)
                ?? new();
            var from = triggerAt.AddMinutes(-JevPrompt.LevelClusterMinutes);
            var radius = JevPrompt.LevelClusterTicks * tick;
            var near = pool.Where(b => b.Date >= from && b.Date <= triggerAt &&
                    Math.Abs(b.Price - support.Value) <= radius)
                .ToList();
            var buy = near.Where(b => b.ActionType == B3WM.Shared.Entity.Ticks2.ActionType.Buy).Sum(b => b.Amount);
            var sell = near.Where(b => b.ActionType == B3WM.Shared.Entity.Ticks2.ActionType.Sale).Sum(b => b.Amount);

            var future = day.Select(ToDto).ToList();
            var label = JevLabels.Direction(future, entryIdx, Horizon, BarrierTicks, tick);
            var decision = rec.Answer.Direcao;
            cases.Add(new ReversalCase
            {
                File = Path.GetFileName(file),
                Symbol = rec.Symbol,
                TimeFrame = rec.TimeFrame,
                TriggerAt = triggerAt,
                Model = rec.Model,
                Decision = decision,
                Confidence = rec.Answer.DirecaoConfidence,
                LevelKind = support.Kind,
                Level = support.Level,
                LevelValue = support.Value,
                DistanceTicks = support.DistanceTicks,
                BubbleCount = near.Count,
                BubbleBuyAmount = buy,
                BubbleSellAmount = sell,
                BubbleNetAmount = buy - sell,
                FutureDirection = label,
                MissedSell = decision == "vender" && label > 0,
            });
        }

        var sample = await RunSampleAsync(cases, dataRoot);
        var report = new StudyReport
        {
            GeneratedAt = DateTime.Now,
            Audits = files.Count,
            SupportCases = cases.Count,
            ReversalCases = cases.Count(c => c.FutureDirection > 0),
            MissedSells = cases.Count(c => c.MissedSell),
            Sample = sample,
            Cases = cases,
        };
        var outDir = Path.Combine(dataRoot, "JevStudy");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, $"reversal-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(outPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[jev-study] {report.Audits} audits | {report.SupportCases} casos de suporte | " +
            $"{report.ReversalCases} reversões | {report.MissedSells} vendas perdidas | {outPath}");
    }

    private static async Task<List<SampleResult>?> RunSampleAsync(
        List<ReversalCase> cases, string dataRoot)
    {
        if (Environment.GetEnvironmentVariable("B3WM_STUDY_SAMPLE") != "1")
            return null;

        var key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")
            ?? Environment.GetEnvironmentVariable("JevKey");
        if (string.IsNullOrWhiteSpace(key))
            return new List<SampleResult> { new() { Error = "sem chave TYPESAFE_API_KEY/JevKey" } };

        var config = new ConfigurationManager
        {
            ["JevKey"] = key,
            ["TypeSafe:Model"] = Environment.GetEnvironmentVariable("TYPESAFE_MODEL") ?? "jev-latest",
        };
        var baseUrl = Environment.GetEnvironmentVariable("TYPESAFE_BASE_URL") ?? "https://api.typesafe.ai";
        var service = new JevService(
            new Factory(new Uri(baseUrl.TrimEnd('/') + "/")),
            config, NullLogger<JevService>.Instance);
        var limit = int.TryParse(Environment.GetEnvironmentVariable("B3WM_STUDY_SAMPLE_N"), out var n)
            ? Math.Clamp(n, 1, 200)
            : 40;

        var results = new List<SampleResult>();
        foreach (var item in cases.Where(c => c.FutureDirection != 0).Take(limit))
        {
            try
            {
                var file = Path.Combine(dataRoot, JevAuditFiles.Dir, item.File);
                var rec = JsonSerializer.Deserialize<JevAuditRecord>(await File.ReadAllTextAsync(file));
                if (rec == null) continue;
                var snap = BuildSnapshot(rec, item.TriggerAt);
                var state = JevPrompt.BuildStateJson(rec.Symbol, rec.TimeFrame, snap);
                var response = await service.AskAsync(state, JevPrompt.BuildQuestions());
                var parsed = JevPrompt.ParseDecision(rec.Symbol, response.Model, response.Body,
                    response.InputTokens, response.OutputTokens, JsonSerializer.Serialize(state),
                    JevWeights.Defaults());
                results.Add(new SampleResult
                {
                    File = item.File,
                    PreviousDecision = item.Decision,
                    NewDecision = parsed.Direcao,
                    NewConfidence = parsed.DirecaoConfidence,
                    FutureDirection = item.FutureDirection,
                    MissedSellAfter = parsed.Direcao == "vender" && item.FutureDirection > 0,
                    InputTokens = parsed.InputTokens,
                    OutputTokens = parsed.OutputTokens,
                });
            }
            catch (Exception ex)
            {
                results.Add(new SampleResult { File = item.File, Error = ex.Message });
            }
        }
        return results;
    }

    private static MarketSnapshot BuildSnapshot(JevAuditRecord rec, DateTime asOf)
    {
        var snap = new MarketSnapshot();
        var candles = Read<List<BarStorageItem>>(rec.Input.Candles) ?? new();
        snap.Layers[SnapshotLayers.Candles] = candles;
        if (candles.Count > 0)
            snap.Layers[SnapshotLayers.ScopeIntraday] = new DateWindow
            {
                From = candles[0].Date, To = candles[^1].Date,
            };

        var pool = Read<List<BubbleStorageItem>>(rec.Input.BubblesPool) ?? new();
        var filtered = Read<List<BubbleStorageItem>>(rec.Input.BubblesFiltered) ?? pool;
        snap.Raw["bubblesPool"] = pool;
        snap.Layers[SnapshotLayers.Bubbles] = filtered;

        var pivIntra = Read<PivotStorageItem>(rec.Input.PivotIntra);
        var pivDaily = Read<PivotStorageItem>(rec.Input.PivotDaily);
        var extIntra = Read<ExtremeStorageItem>(rec.Input.ExtremesIntra);
        var extDaily = Read<ExtremeStorageItem>(rec.Input.ExtremesDaily);
        if (pivIntra != null) snap.Layers[SnapshotLayers.PivotIntra] = pivIntra;
        if (pivDaily != null) snap.Layers[SnapshotLayers.PivotDaily] = pivDaily;
        if (extIntra != null) snap.Layers[SnapshotLayers.ExtremesIntra] = extIntra;
        if (extDaily != null) snap.Layers[SnapshotLayers.ExtremesDaily] = extDaily;
        var volume = Read<VolumeLevelStorageItem>(rec.Input.Volume);
        if (volume != null) snap.Layers[SnapshotLayers.Volume] = volume;
        var structs = Read<List<StructureStorageItem>>(rec.Input.Structures);
        if (structs != null) snap.Layers[SnapshotLayers.Structures] = structs;
        var daily = Read<List<BarStorageItem>>(rec.Input.DailyCandles);
        if (daily != null) snap.Layers[SnapshotLayers.DailyCandles] = daily;
        var dailyVol = Read<VolumeLevelStorageItem>(rec.Input.DailyVolume);
        if (dailyVol != null) snap.Layers[SnapshotLayers.DailyVolume] = dailyVol;
        if (rec.Input.Vwap is { } vwap) snap.Layers[SnapshotLayers.Vwap] = vwap;

        var cluster = JevPrompt.SelectLevelClusterBubbles(snap, asOf);
        if (cluster.Count > 0)
            snap.Layers[SnapshotLayers.Bubbles] =
                B3WM.Services.Strategies.JevAnalysisStrategy.MergeBubbles(filtered, cluster);
        return snap;
    }

    private static SupportLevel? NearestSupport(JevAuditRecord rec, List<BarStorageItem> candles, double tick)
    {
        var last = candles[^1].Close;
        var recent = candles.TakeLast(6).ToList();
        var candidates = new List<SupportLevel>();
        var pivot = Read<PivotStorageItem>(rec.Input.PivotIntra);
        if (pivot != null)
        {
            foreach (var lvl in pivot.Levels.Where(l =>
                         l.Key.StartsWith("S", StringComparison.OrdinalIgnoreCase)))
                candidates.Add(new SupportLevel("pivot", lvl.Key, lvl.Value));
        }
        var ext = Read<ExtremeStorageItem>(rec.Input.ExtremesIntra);
        if (ext != null)
        {
            foreach (var e in ext.Extremes.Where(e => e.Type == ExtremeType.Valley))
                candidates.Add(new SupportLevel("vale", "vale", e.Position));
        }

        return candidates
            .Select(c => c with
            {
                DistanceTicks = Math.Abs(last - c.Value) / tick,
                TouchedRecently = recent.Any(b => b.Low <= c.Value && c.Value <= b.High),
            })
            .Where(c => c.DistanceTicks <= LevelTicks && c.TouchedRecently)
            .OrderBy(c => c.DistanceTicks)
            .FirstOrDefault();
    }

    private static T? Read<T>(JsonElement? element) where T : class =>
        element is { } e ? JsonSerializer.Deserialize<T>(e.GetRawText()) : null;

    private static int LastIndexAtOrBefore(IReadOnlyList<BarStorageItem> bars, DateTime at)
    {
        var idx = -1;
        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i].Date <= at) idx = i;
            else break;
        }
        return idx;
    }

    private static JevCandleDto ToDto(BarStorageItem b) => new()
    {
        Date = b.Date, Open = b.Open, High = b.High, Low = b.Low,
        Close = b.Close, Volume = b.Volume, Vwap = b.VwapPrice,
    };

    private sealed class Keeper(string root) : DataKeeperBase(null)
    {
        protected override string RootDirectory => root;
    }

    private sealed class Factory(Uri baseUri) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new() { BaseAddress = baseUri };
    }

    private sealed record SupportLevel(
        string Kind, string Level, double Value,
        double DistanceTicks = 0, bool TouchedRecently = false);

    private sealed class ReversalCase
    {
        public string File { get; set; } = "";
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; }
        public DateTime TriggerAt { get; set; }
        public string Model { get; set; } = "";
        public string Decision { get; set; } = "";
        public double Confidence { get; set; }
        public string LevelKind { get; set; } = "";
        public string Level { get; set; } = "";
        public double LevelValue { get; set; }
        public double DistanceTicks { get; set; }
        public int BubbleCount { get; set; }
        public decimal BubbleBuyAmount { get; set; }
        public decimal BubbleSellAmount { get; set; }
        public decimal BubbleNetAmount { get; set; }
        public int FutureDirection { get; set; }
        public bool MissedSell { get; set; }
    }

    private sealed class SampleResult
    {
        public string File { get; set; } = "";
        public string PreviousDecision { get; set; } = "";
        public string NewDecision { get; set; } = "";
        public double NewConfidence { get; set; }
        public int FutureDirection { get; set; }
        public bool MissedSellAfter { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public string Error { get; set; } = "";
    }

    private sealed class StudyReport
    {
        public DateTime GeneratedAt { get; set; }
        public int Audits { get; set; }
        public int SupportCases { get; set; }
        public int ReversalCases { get; set; }
        public int MissedSells { get; set; }
        public List<SampleResult>? Sample { get; set; }
        public List<ReversalCase> Cases { get; set; } = new();
    }
}
