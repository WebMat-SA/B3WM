using B3WM.Shared.Models;

namespace B3WM.Services.Core
{
    /// <summary>
    /// Pivot Tradicional do Profit (só este método, issue #14):
    /// P = (H + L + C) / 3, sem variante (O+H+L+C)/4, sem Woodie/DeMark/
    /// Fibonacci/Camarilla. Funções puras: nenhum estado ao vivo, nenhum
    /// timer/broadcast — o DataController só lê os arquivos 1440 e devolve
    /// o snapshot estático do dia atual (ou último pregão).
    /// </summary>
    public static class PivotService
    {
        public const int MinLines = 2;
        public const int MaxLines = 5;

        public static int ClampLineCount(int lineCount) =>
            Math.Clamp(lineCount, MinLines, MaxLines);

        /// <summary>
        /// Calcula os níveis tradicionais a partir do HLC da fonte.
        /// lineCount = pares por lado (2 = P+R1/R2+S1/S2, ..., 5 = até R5/S5).
        /// </summary>
        public static List<PivotLevel> ComputeTraditional(double high, double low, double close, int lineCount)
        {
            if (!double.IsFinite(high) || !double.IsFinite(low) || !double.IsFinite(close))
                throw new ArgumentException("HLC must be finite numbers.");
            if (high < low)
                throw new ArgumentException("High must be >= Low.");
            var n = ClampLineCount(lineCount);

            var p = (high + low + close) / 3.0;
            var range = high - low;
            var r1 = 2 * p - low;
            var s1 = 2 * p - high;
            var r2 = p + range;
            var s2 = p - range;
            var r3 = high + 2 * (p - low);
            var s3 = low - 2 * (high - p);
            // Extensão 4/5 por range (convenção travada na issue #14).
            var r4 = r3 + range;
            var s4 = s3 - range;
            var r5 = r4 + range;
            var s5 = s4 - range;

            var levels = new List<PivotLevel>
            {
                new() { Key = "P", Value = p },
                new() { Key = "R1", Value = r1 },
                new() { Key = "S1", Value = s1 },
                new() { Key = "R2", Value = r2 },
                new() { Key = "S2", Value = s2 },
            };
            if (n >= 3)
            {
                levels.Add(new() { Key = "R3", Value = r3 });
                levels.Add(new() { Key = "S3", Value = s3 });
            }
            if (n >= 4)
            {
                levels.Add(new() { Key = "R4", Value = r4 });
                levels.Add(new() { Key = "S4", Value = s4 });
            }
            if (n >= 5)
            {
                levels.Add(new() { Key = "R5", Value = r5 });
                levels.Add(new() { Key = "S5", Value = s5 });
            }
            return levels;
        }

        /// <summary>
        /// Fonte INTRADAY (fiel ao Profit): última barra 1440 estritamente
        /// anterior à sessão exibida (D-1). Barras ordenadas por data.
        /// Retorna null quando não há D-1.
        /// </summary>
        public static BarStorageItem? ResolveIntradaySource(
            IReadOnlyList<BarStorageItem> dailyBarsOrdered, DateTime sessionDate)
        {
            var target = sessionDate.Date;
            BarStorageItem? best = null;
            foreach (var b in dailyBarsOrdered)
            {
                if (b.Date.Date < target && (best == null || b.Date > best.Date))
                    best = b;
            }
            return best;
        }

        /// <summary>
        /// Fonte DAILY (fiel ao Profit: desloca 1 período superior):
        /// agrega os até 5 pregões 1440 anteriores à segunda-feira da semana
        /// da sessão (H=máx, L=mín, C=fechamento do último dia da semana
        /// anterior). Sem semana anterior → null (caller faz fallback D-1).
        /// </summary>
        public static (double High, double Low, double Close)? ResolveDailySource(
            IReadOnlyList<BarStorageItem> dailyBarsOrdered, DateTime sessionDate)
        {
            var monday = StartOfWeekMonday(sessionDate.Date);
            var prev = dailyBarsOrdered
                .Where(b => b.Date.Date < monday)
                .OrderBy(b => b.Date)
                .ToList();
            if (prev.Count == 0)
                return null;
            // Últimos 5 pregões antes da semana atual (= semana anterior).
            var week = prev.Skip(Math.Max(0, prev.Count - 5)).ToList();
            return (
                High: week.Max(b => b.High),
                Low: week.Min(b => b.Low),
                Close: week.Last().Close
            );
        }

        public static DateTime StartOfWeekMonday(DateTime date)
        {
            // Sunday=0..Saturday=6 → offset até segunda.
            var offset = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-offset);
        }

        /// <summary>
        /// Sessão exibida: o próprio target quando há barra 1440 nele;
        /// senão o último pregão com dados (para "hoje sem pregão ainda").
        /// </summary>
        public static DateTime? ResolveSessionDate(
            IReadOnlyList<BarStorageItem> dailyBarsOrdered, DateTime target)
        {
            var t = target.Date;
            var ordered = dailyBarsOrdered.OrderBy(b => b.Date).ToList();
            if (ordered.Any(b => b.Date.Date == t))
                return t;
            var prev = ordered.Where(b => b.Date.Date < t).ToList();
            if (prev.Count > 0)
                return prev.Last().Date.Date;
            // Sem nada antes: se só há futuro (servidor novo), usa o mais antigo.
            return ordered.Count > 0 ? ordered.First().Date.Date : null;
        }

        /// <summary>
        /// Lê as barras 1440 persistidas no intervalo (mesmo nome de arquivo
        /// do GetBarRange com timeFrame=1440). Dias ausentes são ignorados.
        /// </summary>
        public static async Task<List<BarStorageItem>> ReadDailyBarsAsync(
            Services.DataKeeperBase keeper, string symbol, DateTime from, DateTime to)
        {
            var f = from.Date;
            var t = to.Date;
            if (t < f)
                (f, t) = (t, f);
            if ((t - f).TotalDays > 365)
                f = t.AddDays(-365);
            var all = new List<BarStorageItem>();
            for (var day = f; day <= t; day = day.AddDays(1))
            {
                var path = $"{symbol}_{nameof(CandleService)}_1440MIN_{day:yyyy-MM-dd}.json";
                List<BarStorageItem>? bars;
                try
                {
                    bars = await keeper.ReadDataAsync<List<BarStorageItem>>(path);
                }
                catch
                {
                    continue;
                }
                if (bars != null && bars.Count > 0)
                    all.AddRange(bars.Where(b => b.TimeFrame == 1440));
            }
            return all.OrderBy(b => b.Date).ToList();
        }
    }
}
