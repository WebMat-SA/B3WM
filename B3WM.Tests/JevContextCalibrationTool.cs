using System.Text.Json;
using B3WM.Services.AI;
using B3WM.Services.Screen;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Tests;

/// <summary>
/// Estudo offline da regra de contexto da Jev. Lê Data/JEV (auditorias reais),
/// reconstrói as features a partir dos dados crus e mede o setup
/// "topo/fundo diário + bolhas filtradas + baixo volume perto de vale +
/// movimento forte" contra os candles futuros do dia.
///
/// Roda só com B3WM_STUDY_CONTEXT=1:
///   B3WM_STUDY_CONTEXT=1 dotnet test B3WM.Tests -c Release --filter JevContextCalibrationTool
/// </summary>
public class JevContextCalibrationTool
{
    private const int Horizon = 10;
    private const double RetraceTicks = 10;
    private const double NewExtremeTicks = 5;
    private static readonly int[] Lookbacks = { 10, 20, 30 };
    private static readonly int[] ZoneNearValues = { 30, 50, 80, 100 };
    private static readonly double[] LowVolumeValues = { 0.20, 0.30, 0.40 };
    private static readonly double[] StrongAtrValues = { 1.5, 2.0, 2.5 };
    private static readonly string[] Variants =
    {
        "current", "low_volume", "absorption", "both", "in_zone", "core_only",
    };

    [Fact]
    public async Task Calibrate_WriteReport()
    {
        if (Environment.GetEnvironmentVariable("B3WM_STUDY_CONTEXT") != "1")
            return;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var dataRoot = Path.Combine(repoRoot, "B3WM", "Data");
        var jevDir = Path.Combine(dataRoot, JevAuditFiles.Dir);
        Assert.True(Directory.Exists(jevDir), "Pasta JEV não encontrada: " + jevDir);

        var files = Directory.GetFiles(jevDir, "*.json").OrderBy(f => f).ToList();
        var cases = new List<Case>();
        var futureCache = new Dictionary<string, List<BarStorageItem>>();
        var sizeInputs = new List<int>();
        var sizeChars = new List<int>();
        var sizeStateChars = new List<int>();
        var sizeStateTokens = new List<int>();
        var skipped = new Dictionary<string, int>
        {
            ["sem_candles"] = 0,
            ["sem_bubbles_filtradas"] = 0,
            ["sem_arquivo_futuro"] = 0,
            ["sem_indice_gatilho"] = 0,
            ["erro_leitura"] = 0,
        };

        foreach (var file in files)
        {
            JevAuditRecord? rec;
            try
            {
                rec = JsonSerializer.Deserialize<JevAuditRecord>(
                    await File.ReadAllTextAsync(file));
            }
            catch
            {
                skipped["erro_leitura"]++;
                continue;
            }
            if (rec == null || rec.Input.Candles is null)
            {
                skipped["sem_candles"]++;
                continue;
            }

            var candles = Read<List<BarStorageItem>>(rec.Input.Candles) ?? new();
            if (candles.Count == 0)
            {
                skipped["sem_candles"]++;
                continue;
            }

            var filtered = Read<List<BubbleStorageItem>>(rec.Input.BubblesFiltered);
            if (filtered == null)
            {
                skipped["sem_bubbles_filtradas"]++;
                continue;
            }

            var tick = Defaults.GetTickSize(rec.Symbol);
            if (tick <= 0)
            {
                skipped["sem_candles"]++;
                continue;
            }

            var triggerAt = rec.TriggerAt == default
                ? candles[^1].Date.AddMinutes(Math.Max(1, rec.TimeFrame))
                : rec.TriggerAt;
            var pool = Read<List<BubbleStorageItem>>(rec.Input.BubblesPool) ?? new();
            var sent = Read<List<BubbleStorageItem>>(rec.Input.BubblesSent)
                ?? filtered;
            var cluster = sent.Count > 0 ? sent : pool;

            var volume = Read<VolumeLevelStorageItem>(rec.Input.Volume);
            var dailyVolume = Read<VolumeLevelStorageItem>(rec.Input.DailyVolume);
            var pivIntra = Read<PivotStorageItem>(rec.Input.PivotIntra);
            var pivDaily = Read<PivotStorageItem>(rec.Input.PivotDaily);
            var extIntra = Read<ExtremeStorageItem>(rec.Input.ExtremesIntra);
            var extDaily = Read<ExtremeStorageItem>(rec.Input.ExtremesDaily);
            var structs = Read<List<StructureStorageItem>>(rec.Input.Structures);
            var dailyCandles = Read<List<BarStorageItem>>(rec.Input.DailyCandles) ?? new();
            var vwap = rec.Input.Vwap;

            var baseOptions = JevFeatureOptions.Default with
            {
                ZoneMaxItems = 12,
                ZoneMaxTicks = 120,
            };
            var zones = JevContextAnalyzer.BuildZones(
                candles, pivIntra, pivDaily, extIntra, extDaily,
                volume, dailyVolume, filtered, cluster,
                triggerAt, tick, baseOptions);

            var moves = new Dictionary<int, JevRecentMove>();
            foreach (var lookback in Lookbacks)
                moves[lookback] = JevContextAnalyzer.BuildRecentMove(
                    candles, tick, baseOptions with { MoveLookback = lookback });

            var dayKey = $"{rec.Symbol}_{rec.TimeFrame}_{rec.DisplayDate:yyyy-MM-dd}";
            if (!futureCache.TryGetValue(dayKey, out var futureBars))
            {
                var path = Path.Combine(dataRoot,
                    $"{rec.Symbol}_CandleService_{rec.TimeFrame}MIN_{rec.DisplayDate:yyyy-MM-dd}.json");
                futureBars = new List<BarStorageItem>();
                if (File.Exists(path))
                {
                    try
                    {
                        futureBars = JsonSerializer.Deserialize<List<BarStorageItem>>(
                            await File.ReadAllTextAsync(path)) ?? new();
                    }
                    catch
                    {
                        futureBars = new List<BarStorageItem>();
                    }
                }
                futureBars = futureBars.OrderBy(b => b.Date).ToList();
                futureCache[dayKey] = futureBars;
            }
            if (futureBars.Count == 0)
            {
                skipped["sem_arquivo_futuro"]++;
                continue;
            }

            var entryIdx = LastIndexAtOrBefore(futureBars, triggerAt);
            if (entryIdx < 0)
            {
                skipped["sem_indice_gatilho"]++;
                continue;
            }

            var entryPrice = futureBars[entryIdx].Close;
            var futureHigh = futureBars
                .Skip(entryIdx + 1)
                .Take(Horizon)
                .Select(b => b.High)
                .DefaultIfEmpty(entryPrice)
                .Max();
            var futureLow = futureBars
                .Skip(entryIdx + 1)
                .Take(Horizon)
                .Select(b => b.Low)
                .DefaultIfEmpty(entryPrice)
                .Min();
            var futureClose = futureBars[
                Math.Min(entryIdx + Horizon, futureBars.Count - 1)].Close;
            var futureDirection = JevLabels.Direction(
                futureBars.Select(ToDto).ToList(), entryIdx,
                Horizon, RetraceTicks, tick);

            var state = BuildStateForSize(rec.Symbol, rec.TimeFrame,
                candles, sent.Count > 0 ? sent : filtered, filtered,
                volume, dailyVolume, dailyCandles, pivIntra, pivDaily,
                extIntra, extDaily, structs, vwap);
            var stateJson = JsonSerializer.Serialize(state);
            sizeStateChars.Add(stateJson.Length);
            sizeStateTokens.Add(JevStateBudget.EstimateTokens(stateJson));

            cases.Add(new Case
            {
                File = Path.GetFileName(file),
                Symbol = rec.Symbol,
                TimeFrame = rec.TimeFrame,
                TriggerAt = triggerAt,
                Price = candles[^1].Close,
                EntryPrice = entryPrice,
                FutureHigh = futureHigh,
                FutureLow = futureLow,
                FutureClose = futureClose,
                TickSize = tick,
                Zones = zones,
                Moves = moves,
                FutureDirection = futureDirection,
                InputTokens = rec.Answer.InputTokens,
                StateChars = rec.Filtered.StateChars,
            });
            if (rec.Answer.InputTokens > 0) sizeInputs.Add(rec.Answer.InputTokens);
            if (rec.Filtered.StateChars > 0) sizeChars.Add(rec.Filtered.StateChars);
        }

        var grid = new List<GridResult>();
        foreach (var variant in Variants)
        foreach (var zoneNear in ZoneNearValues)
        foreach (var lowVolume in LowVolumeValues)
        foreach (var strongAtr in StrongAtrValues)
        foreach (var lookback in Lookbacks)
        {
            var options = JevFeatureOptions.Default with
            {
                ZoneNearTicks = zoneNear,
                LowVolumeRank = lowVolume,
                StrongAtr = strongAtr,
                MoveLookback = lookback,
            };
            grid.Add(Evaluate(cases, options, variant));
        }

        var eligible = grid.Where(g => g.Setups >= 20).ToList();
        if (eligible.Count == 0) eligible = grid;
        var bestF1 = eligible
            .OrderByDescending(g => g.F1)
            .ThenByDescending(g => g.Precision)
            .FirstOrDefault();
        var bestReversalFirst = eligible
            .OrderByDescending(g => g.ReversalFirstF1)
            .ThenByDescending(g => g.ReversalFirstPrecision)
            .FirstOrDefault();
        var bestNetAgainst = eligible
            .OrderByDescending(g => g.NetAgainstF1)
            .ThenByDescending(g => g.NetAgainstPrecision)
            .FirstOrDefault();
        var bestLossF1 = eligible
            .OrderByDescending(g => g.LossF1)
            .ThenByDescending(g => g.LossPrecision)
            .FirstOrDefault();
        var bestPrecision = eligible
            .Where(g => g.ReversalFirstRecall >= 0.05)
            .OrderByDescending(g => g.ReversalFirstPrecision)
            .ThenByDescending(g => g.ReversalFirstF1)
            .FirstOrDefault()
            ?? bestReversalFirst;

        var baseline = Evaluate(cases, JevFeatureOptions.Default with
        {
            ZoneMaxItems = 12,
            ZoneMaxTicks = 120,
        }, "current");
        var bestSelection = bestReversalFirst ?? bestF1;
        var bestOptions = bestSelection == null
            ? JevFeatureOptions.Default
            : JevFeatureOptions.Default with
            {
                ZoneNearTicks = bestSelection.ZoneNearTicks,
                LowVolumeRank = bestSelection.LowVolumeRank,
                StrongAtr = bestSelection.StrongAtr,
                MoveLookback = bestSelection.MoveLookback,
            };
        var bestVariant = bestSelection?.Variant ?? "current";

        var report = new CalibrationReport
        {
            GeneratedAt = DateTime.Now,
            Audits = files.Count,
            Cases = cases.Count,
            Skipped = skipped,
            Options = JevFeatureOptions.Default,
            Baseline = baseline,
            BestF1 = bestF1,
            BestReversalFirst = bestReversalFirst,
            BestNetAgainst = bestNetAgainst,
            BestLossF1 = bestLossF1,
            BestPrecision = bestPrecision,
            BySymbol = BySymbol(cases, bestOptions, bestVariant),
            Samples = Samples(cases, bestOptions, bestVariant),
            Size = new SizeStats
            {
                InputTokens = Stats(sizeInputs),
                StateChars = Stats(sizeChars),
                ScopedStateChars = Stats(sizeStateChars),
                ScopedStateTokens = Stats(sizeStateTokens),
            },
            Grid = grid,
        };

        var outDir = Path.Combine(dataRoot, "JevStudy");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir,
            $"context-calibration-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(outPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

        Console.WriteLine(
            $"[jev-context-study] audits={report.Audits} cases={report.Cases} " +
            $"reversalFirst[{bestReversalFirst?.Variant}] " +
            $"P={bestReversalFirst?.ReversalFirstPrecision:F3} " +
            $"R={bestReversalFirst?.ReversalFirstRecall:F3} " +
            $"F1={bestReversalFirst?.ReversalFirstF1:F3} " +
            $"set={bestReversalFirst?.Setups} " +
            $"zn={bestReversalFirst?.ZoneNearTicks} " +
            $"atr={bestReversalFirst?.StrongAtr:F1} " +
            $"lb={bestReversalFirst?.MoveLookback} | " +
            $"netAgainst[{bestNetAgainst?.Variant}] " +
            $"P={bestNetAgainst?.NetAgainstPrecision:F3} " +
            $"R={bestNetAgainst?.NetAgainstRecall:F3} " +
            $"F1={bestNetAgainst?.NetAgainstF1:F3} " +
            $"set={bestNetAgainst?.Setups} | " +
            $"retrace[{bestF1?.Variant}] F1={bestF1?.F1:F3} " +
            $"set={bestF1?.Setups} | " +
            $"loss[{bestLossF1?.Variant}] F1={bestLossF1?.LossF1:F3} " +
            $"set={bestLossF1?.Setups} | " +
            $"stateTokens p50={report.Size.ScopedStateTokens.P50} " +
            $"p95={report.Size.ScopedStateTokens.P95} " +
            $"max={report.Size.ScopedStateTokens.Max} | report={outPath}");
    }

    private static GridResult Evaluate(
        IReadOnlyList<Case> cases, JevFeatureOptions options, string variant)
    {
        var result = new GridResult
        {
            Variant = variant,
            ZoneNearTicks = options.ZoneNearTicks,
            LowVolumeRank = options.LowVolumeRank,
            StrongAtr = options.StrongAtr,
            MoveLookback = options.MoveLookback,
        };

        foreach (var c in cases)
        {
            if (!c.Moves.TryGetValue(options.MoveLookback, out var move)) continue;
            var priorSign = Sign(move.Direction);
            if (priorSign == 0 || !JevContextAnalyzer.IsStrongMove(move, options))
                continue;

            result.Population++;
            var retraceTicks = priorSign > 0
                ? (c.EntryPrice - c.FutureLow) / c.TickSize
                : (c.FutureHigh - c.EntryPrice) / c.TickSize;
            var newExtremeTicks = priorSign > 0
                ? (c.FutureHigh - c.EntryPrice) / c.TickSize
                : (c.EntryPrice - c.FutureLow) / c.TickSize;
            var netAgainstTicks = priorSign > 0
                ? (c.EntryPrice - c.FutureClose) / c.TickSize
                : (c.FutureClose - c.EntryPrice) / c.TickSize;
            var reversalRetrace = retraceTicks >= RetraceTicks;
            var reversalFirst = c.FutureDirection == -priorSign;
            var noContinuation = c.FutureDirection != priorSign;
            var continuationFirst = c.FutureDirection == priorSign;
            var netAgainst = netAgainstTicks >= RetraceTicks;
            var loss = newExtremeTicks < NewExtremeTicks
                && netAgainstTicks >= NewExtremeTicks;

            if (reversalRetrace) result.Positives++;
            if (loss) result.LossPositives++;
            if (continuationFirst) result.ContinuationPositives++;
            if (reversalFirst) result.ReversalFirstPositives++;
            if (netAgainst) result.NetAgainstPositives++;
            if (noContinuation) result.NoContinuationPositives++;

            var confluence = JevContextAnalyzer.EvaluateConfluence(
                c.Zones, move, options, c.Price);
            if (MatchVariant(variant, confluence, move, options))
            {
                result.Setups++;
                if (reversalRetrace) result.Tp++;
                else result.Fp++;
                if (loss) result.LossTp++;
                if (continuationFirst) result.ContinuationTp++;
                if (reversalFirst) result.ReversalFirstTp++;
                if (netAgainst) result.NetAgainstTp++;
                if (noContinuation) result.NoContinuationTp++;
            }
            else
            {
                if (reversalRetrace) result.Fn++;
                if (loss) result.LossFn++;
                if (reversalFirst) result.ReversalFirstFn++;
                if (netAgainst) result.NetAgainstFn++;
                if (noContinuation) result.NoContinuationFn++;
            }
        }

        result.Precision = result.Setups > 0
            ? (double)result.Tp / result.Setups : 0;
        result.Recall = result.Positives > 0
            ? (double)result.Tp / result.Positives : 0;
        result.F1 = F1(result.Precision, result.Recall);
        result.ReversalPrecision = result.Precision;
        result.ReversalRecall = result.Recall;
        result.LossPrecision = result.Setups > 0
            ? (double)result.LossTp / result.Setups : 0;
        result.LossRecall = result.LossPositives > 0
            ? (double)result.LossTp / result.LossPositives : 0;
        result.LossF1 = F1(result.LossPrecision, result.LossRecall);
        result.ReversalFirstPrecision = result.Setups > 0
            ? (double)result.ReversalFirstTp / result.Setups : 0;
        result.ReversalFirstRecall = result.ReversalFirstPositives > 0
            ? (double)result.ReversalFirstTp / result.ReversalFirstPositives : 0;
        result.ReversalFirstF1 = F1(
            result.ReversalFirstPrecision, result.ReversalFirstRecall);
        result.NetAgainstPrecision = result.Setups > 0
            ? (double)result.NetAgainstTp / result.Setups : 0;
        result.NetAgainstRecall = result.NetAgainstPositives > 0
            ? (double)result.NetAgainstTp / result.NetAgainstPositives : 0;
        result.NetAgainstF1 = F1(
            result.NetAgainstPrecision, result.NetAgainstRecall);
        result.NoContinuationPrecision = result.Setups > 0
            ? (double)result.NoContinuationTp / result.Setups : 0;
        result.NoContinuationRecall = result.NoContinuationPositives > 0
            ? (double)result.NoContinuationTp / result.NoContinuationPositives : 0;
        result.NoContinuationF1 = F1(
            result.NoContinuationPrecision, result.NoContinuationRecall);
        return result;
    }

    private static bool MatchVariant(
        string variant, JevConfluence c, JevRecentMove move,
        JevFeatureOptions options)
    {
        var core = move.Direction != "lateral"
            && JevContextAnalyzer.IsStrongMove(move, options)
            && c.OppositeDailyZone
            && c.FilteredBubblesAtZone;
        return variant switch
        {
            "core_only" => core,
            "low_volume" => core && c.LowVolumeNearValley,
            "absorption" => core && c.AbsorptionSide != "none",
            "both" => core && c.LowVolumeNearValley
                && c.AbsorptionSide != "none",
            "in_zone" => core && c.OppositeDailyZoneInRange
                && (c.LowVolumeNearValley || c.AbsorptionSide != "none"),
            _ => core && (c.LowVolumeNearValley || c.AbsorptionSide != "none"),
        };
    }

    private static double F1(double precision, double recall) =>
        precision + recall > 0
            ? 2 * precision * recall / (precision + recall)
            : 0;

    private static Dictionary<string, SymbolStats> BySymbol(
        IReadOnlyList<Case> cases, JevFeatureOptions options, string variant)
    {
        var output = new Dictionary<string, SymbolStats>();
        foreach (var group in cases.GroupBy(c => c.Symbol))
        {
            var stats = new SymbolStats();
            foreach (var c in group)
            {
                if (!c.Moves.TryGetValue(options.MoveLookback, out var move)) continue;
                var priorSign = Sign(move.Direction);
                if (priorSign == 0 || !JevContextAnalyzer.IsStrongMove(move, options))
                    continue;
                stats.Population++;
                var retraceTicks = priorSign > 0
                    ? (c.EntryPrice - c.FutureLow) / c.TickSize
                    : (c.FutureHigh - c.EntryPrice) / c.TickSize;
                var newExtremeTicks = priorSign > 0
                    ? (c.FutureHigh - c.EntryPrice) / c.TickSize
                    : (c.EntryPrice - c.FutureLow) / c.TickSize;
                var netAgainstTicks = priorSign > 0
                    ? (c.EntryPrice - c.FutureClose) / c.TickSize
                    : (c.FutureClose - c.EntryPrice) / c.TickSize;
                var reversal = retraceTicks >= RetraceTicks;
                var reversalFirst = c.FutureDirection == -priorSign;
                var netAgainst = netAgainstTicks >= RetraceTicks;
                var loss = newExtremeTicks < NewExtremeTicks
                    && netAgainstTicks >= NewExtremeTicks;
                if (reversal) stats.Positives++;
                if (loss) stats.LossPositives++;
                if (reversalFirst) stats.ReversalFirstPositives++;
                if (netAgainst) stats.NetAgainstPositives++;

                var confluence = JevContextAnalyzer.EvaluateConfluence(
                    c.Zones, move, options, c.Price);
                if (MatchVariant(variant, confluence, move, options))
                {
                    stats.Setups++;
                    if (reversal) stats.Tp++;
                    else stats.Fp++;
                    if (loss) stats.LossTp++;
                    if (reversalFirst) stats.ReversalFirstTp++;
                    if (netAgainst) stats.NetAgainstTp++;
                }
                else
                {
                    if (reversal) stats.Fn++;
                    if (loss) stats.LossFn++;
                    if (reversalFirst) stats.ReversalFirstFn++;
                    if (netAgainst) stats.NetAgainstFn++;
                }
            }
            stats.Precision = stats.Setups > 0
                ? (double)stats.Tp / stats.Setups : 0;
            stats.Recall = stats.Positives > 0
                ? (double)stats.Tp / stats.Positives : 0;
            stats.F1 = F1(stats.Precision, stats.Recall);
            stats.LossPrecision = stats.Setups > 0
                ? (double)stats.LossTp / stats.Setups : 0;
            stats.LossRecall = stats.LossPositives > 0
                ? (double)stats.LossTp / stats.LossPositives : 0;
            stats.LossF1 = F1(stats.LossPrecision, stats.LossRecall);
            stats.ReversalFirstPrecision = stats.Setups > 0
                ? (double)stats.ReversalFirstTp / stats.Setups : 0;
            stats.ReversalFirstRecall = stats.ReversalFirstPositives > 0
                ? (double)stats.ReversalFirstTp / stats.ReversalFirstPositives : 0;
            stats.ReversalFirstF1 = F1(
                stats.ReversalFirstPrecision, stats.ReversalFirstRecall);
            stats.NetAgainstPrecision = stats.Setups > 0
                ? (double)stats.NetAgainstTp / stats.Setups : 0;
            stats.NetAgainstRecall = stats.NetAgainstPositives > 0
                ? (double)stats.NetAgainstTp / stats.NetAgainstPositives : 0;
            stats.NetAgainstF1 = F1(
                stats.NetAgainstPrecision, stats.NetAgainstRecall);
            output[group.Key] = stats;
        }
        return output;
    }

    private static List<SampleCase> Samples(
        IReadOnlyList<Case> cases, JevFeatureOptions options, string variant)
    {
        var output = new List<SampleCase>();
        foreach (var c in cases)
        {
            if (!c.Moves.TryGetValue(options.MoveLookback, out var move)) continue;
            var priorSign = Sign(move.Direction);
            if (priorSign == 0 || !JevContextAnalyzer.IsStrongMove(move, options))
                continue;
            var confluence = JevContextAnalyzer.EvaluateConfluence(
                c.Zones, move, options, c.Price);
            if (!MatchVariant(variant, confluence, move, options)) continue;

            var retraceTicks = priorSign > 0
                ? (c.EntryPrice - c.FutureLow) / c.TickSize
                : (c.FutureHigh - c.EntryPrice) / c.TickSize;
            var newExtremeTicks = priorSign > 0
                ? (c.FutureHigh - c.EntryPrice) / c.TickSize
                : (c.EntryPrice - c.FutureLow) / c.TickSize;
            var netAgainstTicks = priorSign > 0
                ? (c.EntryPrice - c.FutureClose) / c.TickSize
                : (c.FutureClose - c.EntryPrice) / c.TickSize;

            output.Add(new SampleCase
            {
                File = c.File,
                Symbol = c.Symbol,
                TimeFrame = c.TimeFrame,
                TriggerAt = c.TriggerAt,
                Price = c.Price,
                Variant = variant,
                ZoneKind = confluence.DailyZoneType,
                ZonePrice = confluence.OppositeDailyZonePrice ?? 0,
                ZoneDistanceTicks = confluence.OppositeDailyZoneDistanceTicks ?? 0,
                ZoneInRange = confluence.OppositeDailyZoneInRange,
                LowVolumeValley = confluence.LowVolumeNearValley,
                LowVolumeRank = confluence.LowVolumeValleyRank ?? 0,
                FilteredBubbleCount = confluence.FilteredBubbleCount,
                Absorption = confluence.AbsorptionSide,
                PriorMove = confluence.PriorMove,
                RetraceTicks = retraceTicks,
                NewExtremeTicks = newExtremeTicks,
                NetAgainstTicks = netAgainstTicks,
                FutureDirection = c.FutureDirection,
                Reversal = retraceTicks >= RetraceTicks,
                ReversalFirst = c.FutureDirection == -priorSign,
                NetAgainst = netAgainstTicks >= RetraceTicks,
                LossOfForce = newExtremeTicks < NewExtremeTicks
                    && netAgainstTicks >= NewExtremeTicks,
                Continuation = newExtremeTicks >= RetraceTicks,
            });
            if (output.Count >= 80) break;
        }
        return output;
    }

    private static JevStateDto BuildStateForSize(
        string symbol,
        int timeFrame,
        IReadOnlyList<BarStorageItem> candles,
        IReadOnlyList<BubbleStorageItem> sent,
        IReadOnlyList<BubbleStorageItem> filtered,
        VolumeLevelStorageItem? volume,
        VolumeLevelStorageItem? dailyVolume,
        IReadOnlyList<BarStorageItem> dailyCandles,
        PivotStorageItem? pivIntra,
        PivotStorageItem? pivDaily,
        ExtremeStorageItem? extIntra,
        ExtremeStorageItem? extDaily,
        IReadOnlyList<StructureStorageItem>? structs,
        double? vwap)
    {
        var snap = new MarketSnapshot();
        snap.Layers[SnapshotLayers.Candles] = candles;
        snap.Layers[SnapshotLayers.Bubbles] = sent;
        snap.Layers[SnapshotLayers.Volume] = volume;
        if (pivIntra != null) snap.Layers[SnapshotLayers.PivotIntra] = pivIntra;
        if (pivDaily != null) snap.Layers[SnapshotLayers.PivotDaily] = pivDaily;
        if (extIntra != null) snap.Layers[SnapshotLayers.ExtremesIntra] = extIntra;
        if (extDaily != null) snap.Layers[SnapshotLayers.ExtremesDaily] = extDaily;
        if (structs != null) snap.Layers[SnapshotLayers.Structures] = structs;
        if (dailyVolume != null) snap.Layers[SnapshotLayers.DailyVolume] = dailyVolume;
        if (dailyCandles.Count > 0)
        {
            snap.Layers[SnapshotLayers.DailyCandles] = dailyCandles;
            snap.Layers[SnapshotLayers.ScopeDaily] = new DateWindow
            {
                From = dailyCandles[0].Date,
                To = dailyCandles[^1].Date,
            };
        }
        if (vwap is { } v) snap.Layers[SnapshotLayers.Vwap] = v;
        if (candles.Count > 0)
            snap.Layers[SnapshotLayers.ScopeIntraday] = new DateWindow
            {
                From = candles[0].Date,
                To = candles[^1].Date,
            };
        snap.Raw["bubblesFiltered"] = filtered;
        return JevPrompt.BuildStateJson(symbol, timeFrame, snap,
            includeVolumeLevels: false, maxCandles: 24, maxBubbles: 20);
    }

    private static StatsBlock Stats(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
            return new StatsBlock();
        var sorted = values.OrderBy(v => v).ToList();
        int At(double p) => sorted[Math.Clamp((int)(sorted.Count * p), 0, sorted.Count - 1)];
        return new StatsBlock
        {
            Count = sorted.Count,
            Min = sorted[0],
            P50 = At(0.50),
            P90 = At(0.90),
            P95 = At(0.95),
            Max = sorted[^1],
            Mean = (int)Math.Round(sorted.Average()),
        };
    }

    private static int Sign(string direction) => direction switch
    {
        "alta" => 1,
        "baixa" => -1,
        _ => 0,
    };

    private static int LastIndexAtOrBefore(
        IReadOnlyList<BarStorageItem> bars, DateTime at)
    {
        var index = -1;
        for (var i = 0; i < bars.Count; i++)
        {
            if (bars[i].Date <= at) index = i;
            else break;
        }
        return index;
    }

    private static JevCandleDto ToDto(BarStorageItem b) => new()
    {
        Date = b.Date,
        Open = b.Open,
        High = b.High,
        Low = b.Low,
        Close = b.Close,
        Volume = b.Volume,
        Vwap = b.VwapPrice,
    };

    private static T? Read<T>(JsonElement? element) where T : class =>
        element is { } e ? JsonSerializer.Deserialize<T>(e.GetRawText()) : null;

    private sealed class Case
    {
        public string File { get; init; } = "";
        public string Symbol { get; init; } = "";
        public int TimeFrame { get; init; }
        public DateTime TriggerAt { get; init; }
        public double Price { get; init; }
        public double EntryPrice { get; init; }
        public double FutureHigh { get; init; }
        public double FutureLow { get; init; }
        public double FutureClose { get; init; }
        public double TickSize { get; init; }
        public IReadOnlyList<JevZoneFeature> Zones { get; init; } =
            Array.Empty<JevZoneFeature>();
        public IReadOnlyDictionary<int, JevRecentMove> Moves { get; init; } =
            new Dictionary<int, JevRecentMove>();
        public int FutureDirection { get; init; }
        public int InputTokens { get; init; }
        public int StateChars { get; init; }
    }

    private sealed class CalibrationReport
    {
        public DateTime GeneratedAt { get; set; }
        public int Audits { get; set; }
        public int Cases { get; set; }
        public Dictionary<string, int> Skipped { get; set; } = new();
        public JevFeatureOptions Options { get; set; } = JevFeatureOptions.Default;
        public GridResult Baseline { get; set; } = new();
        public GridResult? BestF1 { get; set; }
        public GridResult? BestReversalFirst { get; set; }
        public GridResult? BestNetAgainst { get; set; }
        public GridResult? BestLossF1 { get; set; }
        public GridResult? BestPrecision { get; set; }
        public Dictionary<string, SymbolStats> BySymbol { get; set; } = new();
        public List<SampleCase> Samples { get; set; } = new();
        public SizeStats Size { get; set; } = new();
        public List<GridResult> Grid { get; set; } = new();
    }

    private sealed class GridResult
    {
        public string Variant { get; set; } = "";
        public int ZoneNearTicks { get; set; }
        public double LowVolumeRank { get; set; }
        public double StrongAtr { get; set; }
        public int MoveLookback { get; set; }
        public int Population { get; set; }
        public int Positives { get; set; }
        public int Setups { get; set; }
        public int Tp { get; set; }
        public int Fp { get; set; }
        public int Fn { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1 { get; set; }
        public double ReversalPrecision { get; set; }
        public double ReversalRecall { get; set; }
        public int LossPositives { get; set; }
        public int LossTp { get; set; }
        public int LossFn { get; set; }
        public double LossPrecision { get; set; }
        public double LossRecall { get; set; }
        public double LossF1 { get; set; }
        public int ContinuationPositives { get; set; }
        public int ContinuationTp { get; set; }
        public int ReversalFirstPositives { get; set; }
        public int ReversalFirstTp { get; set; }
        public int ReversalFirstFn { get; set; }
        public double ReversalFirstPrecision { get; set; }
        public double ReversalFirstRecall { get; set; }
        public double ReversalFirstF1 { get; set; }
        public int NetAgainstPositives { get; set; }
        public int NetAgainstTp { get; set; }
        public int NetAgainstFn { get; set; }
        public double NetAgainstPrecision { get; set; }
        public double NetAgainstRecall { get; set; }
        public double NetAgainstF1 { get; set; }
        public int NoContinuationPositives { get; set; }
        public int NoContinuationTp { get; set; }
        public int NoContinuationFn { get; set; }
        public double NoContinuationPrecision { get; set; }
        public double NoContinuationRecall { get; set; }
        public double NoContinuationF1 { get; set; }
    }

    private sealed class SymbolStats
    {
        public int Population { get; set; }
        public int Positives { get; set; }
        public int Setups { get; set; }
        public int Tp { get; set; }
        public int Fp { get; set; }
        public int Fn { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1 { get; set; }
        public int LossPositives { get; set; }
        public int LossTp { get; set; }
        public int LossFn { get; set; }
        public double LossPrecision { get; set; }
        public double LossRecall { get; set; }
        public double LossF1 { get; set; }
        public int ReversalFirstPositives { get; set; }
        public int ReversalFirstTp { get; set; }
        public int ReversalFirstFn { get; set; }
        public double ReversalFirstPrecision { get; set; }
        public double ReversalFirstRecall { get; set; }
        public double ReversalFirstF1 { get; set; }
        public int NetAgainstPositives { get; set; }
        public int NetAgainstTp { get; set; }
        public int NetAgainstFn { get; set; }
        public double NetAgainstPrecision { get; set; }
        public double NetAgainstRecall { get; set; }
        public double NetAgainstF1 { get; set; }
    }

    private sealed class SampleCase
    {
        public string File { get; set; } = "";
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; }
        public DateTime TriggerAt { get; set; }
        public double Price { get; set; }
        public string Variant { get; set; } = "";
        public string ZoneKind { get; set; } = "";
        public double ZonePrice { get; set; }
        public double ZoneDistanceTicks { get; set; }
        public bool ZoneInRange { get; set; }
        public bool LowVolumeValley { get; set; }
        public double LowVolumeRank { get; set; }
        public int FilteredBubbleCount { get; set; }
        public string Absorption { get; set; } = "";
        public string PriorMove { get; set; } = "";
        public double RetraceTicks { get; set; }
        public double NewExtremeTicks { get; set; }
        public double NetAgainstTicks { get; set; }
        public int FutureDirection { get; set; }
        public bool Reversal { get; set; }
        public bool ReversalFirst { get; set; }
        public bool NetAgainst { get; set; }
        public bool LossOfForce { get; set; }
        public bool Continuation { get; set; }
    }

    private sealed class SizeStats
    {
        public StatsBlock InputTokens { get; set; } = new();
        public StatsBlock StateChars { get; set; } = new();
        /// <summary>State novo (context + cortes) estimado em chars.</summary>
        public StatsBlock ScopedStateChars { get; set; } = new();
        /// <summary>State novo estimado em tokens (1 token/char).</summary>
        public StatsBlock ScopedStateTokens { get; set; } = new();
    }

    private sealed class StatsBlock
    {
        public int Count { get; set; }
        public int Min { get; set; }
        public int P50 { get; set; }
        public int P90 { get; set; }
        public int P95 { get; set; }
        public int Max { get; set; }
        public int Mean { get; set; }
    }
}
