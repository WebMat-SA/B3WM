using System.Text.Json;
using System.Text.RegularExpressions;
using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Tests;

/// <summary>
/// Utilitário TEMPORÁRIO (não roda por padrão): reescreve as auditorias Jev
/// antigas de B3WM/Data/JEV enriquecendo o bloco Input com os dados reais,
/// reconstruídos dos arquivos do dia. Roda só com a env var B3WM_BACKFILL=1.
///
/// Uso:
///   B3WM_BACKFILL=1 dotnet test B3WM.Tests -c Release --filter JevAuditBackfill
///
/// Fidelidade: aproximada do instante da chamada (candles/bolhas recortados
/// por TriggerAt). Volume/extremes/vwap persistidos são o acumulado final do
/// dia. ScreenSpec/BubblesFiltered/Position não foram salvos → ficam null.
/// Backup automático em {arquivo}.pre-format.bak antes de sobrescrever.
/// </summary>
public class JevAuditBackfill
{
    [Fact]
    public async Task OverwriteJevAudits_WithDetailedInput()
    {
        if (Environment.GetEnvironmentVariable("B3WM_BACKFILL") != "1")
            return;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var dataRoot = Path.Combine(repoRoot, "B3WM", "Data");
        var jevDir = Path.Combine(dataRoot, JevAuditFiles.Dir);
        Assert.True(Directory.Exists(jevDir), "Pasta JEV não encontrada: " + jevDir);

        var keeper = new Keeper(dataRoot);
        var files = Directory.GetFiles(jevDir, "*.json").OrderBy(f => f).ToList();
        Assert.NotEmpty(files);

        var processed = 0;
        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file);
            var rec = JsonSerializer.Deserialize<JevAuditRecord>(text);
            if (rec == null) continue;

            await EnrichAsync(keeper, rec);

            var bak = file + ".pre-format.bak";
            if (!File.Exists(bak))
                File.Copy(file, bak);

            await keeper.WriteDataAsync(
                $"{JevAuditFiles.Dir}/{Path.GetFileName(file)}", rec);
            processed++;
            Console.WriteLine($"[backfill] {Path.GetFileName(file)} enriquecido.");
        }

        Console.WriteLine($"[backfill] {processed} arquivo(s) reescritos em {jevDir}.");
    }

    private static async Task EnrichAsync(DataKeeperBase keeper, JevAuditRecord rec)
    {
        var sym = rec.Symbol;
        var date = rec.DisplayDate.Date;
        var tf = rec.TimeFrame;
        var trigger = rec.TriggerAt == default ? date.AddDays(1) : rec.TriggerAt;
        var lookC = rec.Input.LookbackCandles > 0 ? rec.Input.LookbackCandles : 60;
        var layers = rec.Input.LayersOn ?? new List<string>();

        // Candles: arquivo do dia, recorte <= TriggerAt, últimos N.
        var candles = await ReadListAsync<BarStorageItem>(keeper,
            $"{sym}_{nameof(CandleService)}_{tf}MIN_{date:yyyy-MM-dd}.json");
        var sentCandles = candles.Where(c => c.Date <= trigger)
            .OrderBy(c => c.Date).TakeLast(lookC).ToList();
        rec.Input.Candles = JevPrompt.ProjectCandles(sentCandles);

        // Bolhas: pool cru do dia recortado <= TriggerAt.
        var pool = await ReadListAsync<BubbleStorageItem>(keeper,
            $"{sym}_{nameof(BubbleService)}_{date:yyyy-MM-dd}.json");
        rec.Input.BubblesPool = JsonSerializer.SerializeToElement(
            pool.Where(b => b.Date <= trigger).OrderBy(b => b.Date).ToList());

        // Bolhas enviadas: parseadas do state dentro do Payload (ground-truth do enviado).
        rec.Input.BubblesSent = JsonSerializer.SerializeToElement(
            ParseSentBubbles(ExtractState(rec.Payload), sym, date));

        // Volume: perfil do dia (acumulado final — ressalva registrada).
        var vol = await keeper.ReadDataAsync<VolumeLevelStorageItem>(
            $"{sym}_{nameof(VolumeService)}_{date:yyyy-MM-dd}.json");
        if (vol is { Volumes.Count: > 0 })
            rec.Input.Volume = JsonSerializer.SerializeToElement(vol);

        // Extremes intraday.
        if (layers.Contains(SnapshotLayers.ExtremesIntra))
        {
            var ex = await keeper.ReadDataAsync<ExtremeStorageItem>(
                $"{sym}_{nameof(ExtremeService)}_{date:yyyy-MM-dd}.json");
            if (ex is { Extremes.Count: > 0 })
                rec.Input.ExtremesIntra = JsonSerializer.SerializeToElement(ex);
        }

        // Extremes diários (default de janela: 60 dias).
        if (layers.Contains(SnapshotLayers.ExtremesDaily))
        {
            var from = date.AddDays(-60);
            var profile = await ExtremeService.BuildDailyProfileFromFiles(keeper, sym, from, date);
            var res = ExtremeDetector.Detect(
                profile.Select(v => v.Price).ToArray(),
                profile.Select(v => (double)v.Total).ToArray(),
                new ExtremeDetectorOptions
                {
                    NoiseSensitivity = Defaults.Extreme.NoiseSensitivity,
                    MinimumProminence = Defaults.Extreme.MinimumProminence,
                });
            if (res.Extremes.Count > 0)
                rec.Input.ExtremesDaily = JsonSerializer.SerializeToElement(new ExtremeStorageItem
                {
                    Date = date, Symbol = sym, PeriodFrom = from, PeriodTo = date,
                    Extremes = res.Extremes, Structures = res.Structures, Statistics = res.Statistics,
                });
        }

        // Structures (borda por TF; tenta as distâncias usuais).
        if (layers.Contains(SnapshotLayers.Structures))
        {
            var all = new List<StructureStorageItem>();
            foreach (var d in new double[] { 405, 250 })
            {
                var s = await ReadListAsync<StructureStorageItem>(keeper,
                    $"{sym}_{nameof(StructureService)}_{tf}MIN_{d}_{date:yyyy-MM-dd}.json");
                if (s.Count > 0) { all.AddRange(s); break; }
            }
            if (all.Count > 0)
                rec.Input.Structures = JsonSerializer.SerializeToElement(all.OrderBy(s => s.Date).ToList());
        }

        // Pivots intraday/diário (lineCount inferido da contagem antiga).
        if (layers.Contains(SnapshotLayers.PivotIntra) || layers.Contains(SnapshotLayers.PivotDaily))
        {
            var daily = await PivotService.ReadDailyBarsAsync(keeper, sym, date.AddDays(-90), date);
            var session = PivotService.ResolveSessionDate(daily, date);
            if (session != null)
            {
                if (layers.Contains(SnapshotLayers.PivotIntra))
                {
                    var src = PivotService.ResolveIntradaySource(daily, session.Value);
                    if (src != null)
                    {
                        var lc = LineCountFrom(rec.Filtered.PivotIntraLevels);
                        rec.Input.PivotIntra = JsonSerializer.SerializeToElement(new PivotStorageItem
                        {
                            Symbol = sym, Date = session.Value, Source = "D-1",
                            High = src.High, Low = src.Low, Close = src.Close, LineCount = lc,
                            Levels = PivotService.ComputeTraditional(src.High, src.Low, src.Close, lc),
                        });
                    }
                }
                if (layers.Contains(SnapshotLayers.PivotDaily))
                {
                    var lc = LineCountFrom(rec.Filtered.PivotDailyLevels);
                    var weekly = PivotService.ResolveDailySource(daily, session.Value);
                    if (weekly != null)
                    {
                        rec.Input.PivotDaily = JsonSerializer.SerializeToElement(new PivotStorageItem
                        {
                            Symbol = sym, Date = session.Value, Source = "W-1",
                            High = weekly.Value.High, Low = weekly.Value.Low, Close = weekly.Value.Close,
                            LineCount = lc,
                            Levels = PivotService.ComputeTraditional(weekly.Value.High, weekly.Value.Low, weekly.Value.Close, lc),
                        });
                    }
                    else
                    {
                        var src = PivotService.ResolveIntradaySource(daily, session.Value);
                        if (src != null)
                            rec.Input.PivotDaily = JsonSerializer.SerializeToElement(new PivotStorageItem
                            {
                                Symbol = sym, Date = session.Value, Source = "D-1",
                                High = src.High, Low = src.Low, Close = src.Close, LineCount = lc,
                                Levels = PivotService.ComputeTraditional(src.High, src.Low, src.Close, lc),
                            });
                    }
                }
            }
        }

        // VWAP do dia até o momento do gatilho.
        var vwapFile = await ReadListAsync<VwapHistoryPoint>(keeper,
            $"{sym}_{nameof(VwapService)}_{date:yyyy-MM-dd}.json");
        var lastVwap = vwapFile.Where(p => p.Minute <= trigger)
            .OrderBy(p => p.Minute).LastOrDefault();
        if (lastVwap is { Vwap: > 0 })
            rec.Input.Vwap = lastVwap.Vwap;

        // Header reconstruído das contagens antigas + layers.
        rec.Input.Header = JsonSerializer.SerializeToElement(BuildHeader(rec, sentCandles, layers));
    }

    private static async Task<List<T>> ReadListAsync<T>(DataKeeperBase keeper, string path) where T : new()
    {
        try { return await keeper.ReadDataAsync<List<T>>(path) ?? new(); }
        catch { return new(); }
    }

    private static HeaderView BuildHeader(
        JevAuditRecord rec, List<BarStorageItem> candles, List<string> layers)
    {
        return new HeaderView
        {
            DisplayDate = rec.DisplayDate,
            Mode = "intraday",
            LastPrice = candles.Count > 0 ? candles[^1].Close : (double?)null,
            VisibleTimeFrames = new() { rec.TimeFrame },
            ThresholdBubble = rec.Input.ThresholdBubble,
            VwapVisible = layers.Contains(SnapshotLayers.Vwap),
            PanelOpen = layers.Contains(SnapshotLayers.ExtremesDaily)
                        || layers.Contains(SnapshotLayers.PivotDaily),
            BubblesTotalDay = rec.Input.BubblesPoolTotal,
            BubblesVisibleDay = rec.Input.BubblesAfterScreenFilter,
        };
    }

    /// <summary>Nível total = 1 + 2*lineCount → lineCount = (n-1)/2 (clamp 2..5).</summary>
    private static int LineCountFrom(int levelCount) =>
        PivotService.ClampLineCount(levelCount <= 0 ? 2 : (levelCount - 1) / 2);

    private static readonly Regex BubbleLine = new(
        @"^- (\d{2}):(\d{2}):(\d{2}) agente=(\d+) lado=(\w+) qtd=([\d.,-]+) preco=([\d.,-]+)\r?$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Extrai o state (objeto) do payload exato; vazio em auditorias antigas.</summary>
    private static string ExtractState(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return "";
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("state", out var state)
                ? state.GetRawText()
                : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static List<BubbleStorageItem> ParseSentBubbles(string state, string symbol, DateTime date)
    {
        var list = new List<BubbleStorageItem>();
        if (string.IsNullOrEmpty(state)) return list;
        foreach (Match m in BubbleLine.Matches(state))
        {
            var at = date.AddHours(int.Parse(m.Groups[1].Value))
                .AddMinutes(int.Parse(m.Groups[2].Value))
                .AddSeconds(int.Parse(m.Groups[3].Value));
            if (!Enum.TryParse<Ticks2.ActionType>(m.Groups[5].Value, true, out var action))
                action = Ticks2.ActionType.Buy;
            list.Add(new BubbleStorageItem
            {
                Symbol = symbol,
                Date = at,
                Agent = int.Parse(m.Groups[4].Value),
                ActionType = action,
                Amount = ParseDec(m.Groups[6].Value),
                Price = (double)ParseDec(m.Groups[7].Value),
            });
        }
        return list;
    }

    private static decimal ParseDec(string raw)
    {
        var norm = raw.Replace(',', '.');
        return decimal.TryParse(norm, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private sealed class Keeper : DataKeeperBase
    {
        private readonly string _root;
        public Keeper(string root) : base(null) => _root = root;
        protected override string RootDirectory => _root;
    }
}
