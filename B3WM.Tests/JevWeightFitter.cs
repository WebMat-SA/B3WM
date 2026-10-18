using System.Text.Json;
using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;

namespace B3WM.Tests;

/// <summary>Regressão logística própria (gradiente) — puro, sem dependências.</summary>
public static class LogisticRegression
{
    /// <summary>
    /// Ajusta p = sigmoid(b0 + Σ bj·xj) por ascensão do gradiente da
    /// log-verossimilhança com regularização L2. Devolve [bias, w0..w(n-1)].
    /// Listas vazias/degeneradas → coeficientes vazios.
    /// </summary>
    public static double[] Fit(
        IReadOnlyList<double[]> x, IReadOnlyList<int> y,
        double l2 = 1.0, int iters = 1000, double lr = 0.5)
    {
        if (x.Count == 0 || y.Count != x.Count || x[0].Length == 0)
            return Array.Empty<double>();

        int n = x.Count, m = x[0].Length;
        var b = new double[m + 1];
        var g = new double[m + 1];

        for (int it = 0; it < iters; it++)
        {
            Array.Clear(g);
            for (int i = 0; i < n; i++)
            {
                var xi = x[i];
                double z = b[0];
                for (int j = 0; j < m; j++) z += b[j + 1] * xi[j];
                double e = 1.0 / (1.0 + Math.Exp(-z)) - y[i];
                g[0] += e;
                for (int j = 0; j < m; j++) g[j + 1] += e * xi[j];
            }
            b[0] -= lr * g[0] / n;
            for (int j = 0; j < m; j++)
                b[j + 1] -= lr * (g[j + 1] / n + l2 * b[j + 1] / n);
        }
        return b;
    }
}

/// <summary>Rótulos do fitter (triple-barrier) a partir de candles futuros.</summary>
public static class JevLabels
{
    /// <summary>
    /// +1 = barreira de alta tocada primeiro, -1 = baixa primeiro, 0 =
    /// nenhuma/ambígua dentro de <paramref name="horizon"/> candles. Barra
    /// que cruza as duas barreiras é ambígua (0). Começa no candle seguinte à
    /// entrada (a entrada é o close do candle do gatilho).
    /// </summary>
    public static int Direction(
        IReadOnlyList<JevCandleDto> c, int entryIdx, int horizon,
        double kTicks, double tickSize)
    {
        if (entryIdx < 0 || entryIdx >= c.Count || tickSize <= 0 || horizon <= 0)
            return 0;

        double entry = c[entryIdx].Close;
        double up = entry + kTicks * tickSize;
        double down = entry - kTicks * tickSize;
        int last = Math.Min(entryIdx + horizon, c.Count - 1);

        for (int i = entryIdx + 1; i <= last; i++)
        {
            bool hitUp = c[i].High >= up;
            bool hitDown = c[i].Low <= down;
            if (hitUp && hitDown) return 0;
            if (hitUp) return 1;
            if (hitDown) return -1;
        }
        return 0;
    }

    /// <summary>
    /// Operabilidade: o preço andou ≥ k ticks (para cima OU para baixo) em até
    /// <paramref name="horizon"/> candles. Diferente da direção, uma barra
    /// ambígua conta como "moveu".
    /// </summary>
    public static bool Moved(
        IReadOnlyList<JevCandleDto> c, int entryIdx, int horizon,
        double kTicks, double tickSize)
    {
        if (entryIdx < 0 || entryIdx >= c.Count || tickSize <= 0 || horizon <= 0)
            return false;

        double entry = c[entryIdx].Close;
        double target = kTicks * tickSize;
        int last = Math.Min(entryIdx + horizon, c.Count - 1);

        for (int i = entryIdx + 1; i <= last; i++)
            if (c[i].High - entry >= target || entry - c[i].Low >= target)
                return true;
        return false;
    }
}

/// <summary>
/// Fitter offline dos pesos da Jev (fase B): a partir das auditorias
/// (criteria respondidos) + candles futuros, rotula (triple-barrier) e ajusta
/// uma regressão logística por grupo. Escreve Data/jev_weights.json, que o
/// <see cref="JevWeights.LoadAsync"/> consome. Não roda por padrão.
/// </summary>
public static class JevWeightFitter
{
    /// <summary>
    /// Converte os coeficientes aprendidos em pesos. Coeficientes ≤ 0 viram 0
    /// (o indicador não contribui); grupo todo-zerado/degenerado mantém o
    /// baseline (nunca devolve grupo vazio).
    /// </summary>
    public static JevWeights ComputeWeights(
        IReadOnlyList<double[]> dirX, IReadOnlyList<int> dirY,
        IReadOnlyList<double[]> opX, IReadOnlyList<int> opY,
        JevWeights? baseline = null)
    {
        var w = baseline ?? JevWeights.Defaults();
        return new JevWeights
        {
            Direction = Map(JevPrompt.DirectionIndicators,
                LogisticRegression.Fit(dirX, dirY), w.Direction),
            Operability = Map(JevPrompt.OperabilityIndicators,
                LogisticRegression.Fit(opX, opY), w.Operability),
            LabelHorizon = w.LabelHorizon,
            LabelTicks = w.LabelTicks,
            ProximityMaxTicks = w.ProximityMaxTicks,
        };
    }

    private static Dictionary<string, double> Map(
        string[] ids, double[] coef, Dictionary<string, double> fallback)
    {
        if (coef.Length != ids.Length + 1)
            return new Dictionary<string, double>(fallback);

        var out_ = new Dictionary<string, double>();
        double sum = 0;
        for (int j = 0; j < ids.Length; j++)
        {
            double v = Math.Max(0, coef[j + 1]);
            out_[ids[j]] = Math.Round(v, 4);
            sum += v;
        }
        return sum <= 1e-9 ? new Dictionary<string, double>(fallback) : out_;
    }
}

/// <summary>
/// Tool gated: lê Data/JEV, rotula com os candles futuros dos arquivos do dia
/// e grava Data/jev_weights.json. Roda só com B3WM_FIT_WEIGHTS=1:
///   B3WM_FIT_WEIGHTS=1 dotnet test B3WM.Tests -c Release --filter JevWeightFitterTool
/// </summary>
public class JevWeightFitterTool
{
    [Fact]
    public async Task FitFromAudits_WriteWeights()
    {
        if (Environment.GetEnvironmentVariable("B3WM_FIT_WEIGHTS") != "1")
            return;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var dataRoot = Path.Combine(repoRoot, "B3WM", "Data");
        var jevDir = Path.Combine(dataRoot, JevAuditFiles.Dir);
        Assert.True(Directory.Exists(jevDir), "Pasta JEV não encontrada: " + jevDir);

        var keeper = new Keeper(dataRoot);
        var baseline = JevWeights.Defaults();
        var bars = new Dictionary<string, List<JevCandleDto>>();

        var dirX = new List<double[]>();
        var dirY = new List<int>();
        var opX = new List<double[]>();
        var opY = new List<int>();
        var skipped = 0;

        foreach (var file in Directory.GetFiles(jevDir, "*.json").OrderBy(f => f))
        {
            JevAuditRecord? rec;
            try { rec = JsonSerializer.Deserialize<JevAuditRecord>(await File.ReadAllTextAsync(file)); }
            catch { continue; }
            if (rec == null || rec.Answer.Criteria.Count == 0) continue;

            var date = rec.DisplayDate.Date;
            var key = $"{rec.Symbol}_{rec.TimeFrame}_{date:yyyy-MM-dd}";
            if (!bars.TryGetValue(key, out var candles))
            {
                var raw = await ReadListAsync<BarStorageItem>(keeper,
                    $"{rec.Symbol}_{nameof(CandleService)}_{rec.TimeFrame}MIN_{date:yyyy-MM-dd}.json");
                candles = raw.OrderBy(b => b.Date).Select(ToDto).ToList();
                bars[key] = candles;
            }
            if (candles.Count == 0) { skipped++; continue; }

            var trigger = rec.TriggerAt == default ? candles[^1].Date : rec.TriggerAt;
            var entry = LastIndexAtOrBefore(candles, trigger);
            if (entry < 0) { skipped++; continue; }

            double tick = Defaults.GetTickSize(rec.Symbol);
            var crit = rec.Answer.Criteria;

            if (HasAll(crit, JevPrompt.OperabilityIndicators))
            {
                opX.Add(Features(crit, JevPrompt.OperabilityIndicators));
                opY.Add(JevLabels.Moved(candles, entry,
                    baseline.LabelHorizon, baseline.LabelTicks, tick) ? 1 : 0);
            }
            if (HasAll(crit, JevPrompt.DirectionIndicators))
            {
                var dir = JevLabels.Direction(candles, entry,
                    baseline.LabelHorizon, baseline.LabelTicks, tick);
                if (dir != 0)
                {
                    dirX.Add(Features(crit, JevPrompt.DirectionIndicators));
                    dirY.Add(dir > 0 ? 1 : 0);
                }
            }
        }

        var weights = JevWeightFitter.ComputeWeights(dirX, dirY, opX, opY, baseline);
        await keeper.WriteDataAsync(JevWeights.FileName, weights);

        Console.WriteLine($"[fit] direção: {dirX.Count} amostras | operabilidade: {opX.Count} | puladas: {skipped}");
        foreach (var (k, v) in weights.Direction) Console.WriteLine($"[fit] dir  {k} = {v}");
        foreach (var (k, v) in weights.Operability) Console.WriteLine($"[fit] op   {k} = {v}");
        if (dirX.Count < 150)
            Console.WriteLine("[fit] AVISO: menos de 150 amostras de direção; pesos pouco confiáveis.");
        if (opX.Count < 150)
            Console.WriteLine("[fit] AVISO: menos de 150 amostras de operabilidade; pesos pouco confiáveis.");
    }

    private static bool HasAll(IReadOnlyDictionary<string, double> crit, string[] ids)
    {
        foreach (var id in ids) if (!crit.ContainsKey(id)) return false;
        return true;
    }

    private static double[] Features(IReadOnlyDictionary<string, double> crit, string[] ids)
    {
        var v = new double[ids.Length];
        for (int i = 0; i < ids.Length; i++) v[i] = crit[ids[i]];
        return v;
    }

    private static int LastIndexAtOrBefore(IReadOnlyList<JevCandleDto> c, DateTime t)
    {
        int idx = -1;
        for (int i = 0; i < c.Count; i++)
        {
            if (c[i].Date <= t) idx = i;
            else break;
        }
        return idx;
    }

    private static JevCandleDto ToDto(BarStorageItem b) => new()
    {
        Date = b.Date, Open = b.Open, High = b.High, Low = b.Low,
        Close = b.Close, Volume = b.Volume, Vwap = b.VwapPrice,
    };

    private static async Task<List<T>> ReadListAsync<T>(DataKeeperBase keeper, string path)
        where T : new()
    {
        try { return await keeper.ReadDataAsync<List<T>>(path) ?? new(); }
        catch { return new(); }
    }

    private sealed class Keeper : DataKeeperBase
    {
        private readonly string _root;
        public Keeper(string root) : base(null) => _root = root;
        protected override string RootDirectory => _root;
    }
}
