using B3WM.Shared.Models;

namespace B3WM.Services.Screen
{
    /// <summary>
    /// Uma mudança de borda de estrutura (up/down) numa data — espelho de
    /// <c>StructureChangeItem</c> do app. <see cref="IsUpMove"/> distingue
    /// topo (new &gt; old) de fundo; usado na escolha da âncora.
    /// </summary>
    public readonly record struct StructureChange(
        DateTime Date, bool IsUp, double OldValue, double NewValue)
    {
        public bool IsUpMove => NewValue > OldValue;
    }

    /// <summary>
    /// Janela intraday resolvida no backend: índices sobre as barras do
    /// timeframe (start incluso, end exclusivo = total) + as datas
    /// correspondentes. <see cref="EndIndex"/> == 0 indica "sem barras".
    /// </summary>
    public readonly record struct IntradayWindowResult(
        int StartIndex, int EndIndex, DateTime From, DateTime To)
    {
        public bool IsEmpty => EndIndex <= 0;
    }

    /// <summary>
    /// Porte fiel da resolução da "janela por estrutura" (automode) que hoje
    /// vive no Flutter (<c>state_service.dart</c>). Funções puras — sem I/O —
    /// para o backend ser a fonte única da janela (app e JEV consomem).
    ///
    /// Referência (Dart):
    ///  - <c>_recomputeStructureChanges</c>, <c>_applyStructureAutoFilter</c>,
    ///    <c>confirmationBarIndex</c>, <c>oppositeResetDate</c>,
    ///    <c>resolveExtremeBarIndex</c>, <c>resolveDailyAnchorDay</c>,
    ///    <c>resolveDailyAutoWindow</c>.
    /// </summary>
    public static class StructureWindowResolver
    {
        private const int DailyTimeFrame = 1440;
        private const int DailyFallbackDays = 60;

        /// <summary>
        /// Mudanças de borda por data (order: mais recente primeiro), a partir
        /// do histórico de estruturas do símbolo/timeframe. Espelha
        /// <c>_recomputeStructureChanges</c>: monta em ordem crescente (up
        /// antes de down em cada print) e depois inverte (reverse), de forma
        /// que a mudança mais recente vira o primeiro elemento.
        /// </summary>
        public static IReadOnlyList<StructureChange> BuildChanges(
            IEnumerable<StructureStorageItem> structures, string symbol, int timeFrame)
        {
            var relevant = structures
                .Where(s => s.Symbol == symbol && s.TimeFrame == timeFrame)
                .OrderBy(s => s.Date)
                .ToList();
            if (relevant.Count < 2)
                return Array.Empty<StructureChange>();

            var changes = new List<StructureChange>();
            for (var i = 1; i < relevant.Count; i++)
            {
                var prev = relevant[i - 1];
                var curr = relevant[i];
                if (curr.UpBorder != prev.UpBorder)
                    changes.Add(new StructureChange(curr.Date, true, prev.UpBorder, curr.UpBorder));
                if (curr.DownBorder != prev.DownBorder)
                    changes.Add(new StructureChange(curr.Date, false, prev.DownBorder, curr.DownBorder));
            }
            changes.Reverse();
            return changes;
        }

        /// <summary>
        /// Última barra com <c>date &lt;= changeDate</c> (0 quando nenhuma).
        /// Espelha <c>confirmationBarIndex</c>.
        /// </summary>
        public static int ConfirmationBarIndex(IReadOnlyList<BarStorageItem> bars, DateTime changeDate)
        {
            var idx = 0;
            for (var i = bars.Count - 1; i >= 0; i--)
            {
                if (bars[i].Date <= changeDate)
                {
                    idx = i;
                    break;
                }
            }
            return idx;
        }

        /// <summary>
        /// Data da mudança de lado oposto mais recente antes de
        /// <paramref name="anchorPos"/> (<c>changesDesc</c> em ordem
        /// mais-recente-primeiro). Null quando não há — busca sem limite.
        /// Espelha <c>oppositeResetDate</c>.
        /// </summary>
        public static DateTime? OppositeResetDate(
            IReadOnlyList<StructureChange> changesDesc, int anchorPos)
        {
            if (anchorPos < 0 || anchorPos >= changesDesc.Count) return null;
            var anchorIsUp = changesDesc[anchorPos].IsUp;
            for (var k = anchorPos + 1; k < changesDesc.Count; k++)
                if (changesDesc[k].IsUp != anchorIsUp)
                    return changesDesc[k].Date;
            return null;
        }

        /// <summary>
        /// Retrocede do candle de confirmação ao candle do PRIMEIRO toque no
        /// extremo (E, incluso na janela). Fonte primária = série aux das
        /// impressões de estrutura; fallback = varredura de high/low; último
        /// fallback = confirmação. Espelha <c>resolveExtremeBarIndex</c>.
        /// </summary>
        public static int ResolveExtremeBarIndex(
            IReadOnlyList<BarStorageItem> bars,
            IReadOnlyList<StructureStorageItem> structures,
            string symbol,
            int timeFrame,
            StructureChange anchor,
            int confirmationIndex,
            int lowerBound,
            double tolerance)
        {
            if (bars.Count == 0) return 0;
            var conf = Math.Clamp(confirmationIndex, 0, bars.Count - 1);
            var lo = Math.Clamp(lowerBound, 0, conf);

            var prints = structures
                .Where(s => s.Symbol == symbol && s.TimeFrame == timeFrame)
                .OrderBy(s => s.Date)
                .ToList();
            if (prints.Count > 0)
            {
                var loDate = bars[lo].Date;
                foreach (var p in prints)
                {
                    if (p.Date < loDate || p.Date > anchor.Date) continue;
                    var aux = anchor.IsUp ? p.UpAuxBorder : p.DownAuxBorder;
                    if (Math.Abs(aux - anchor.NewValue) <= tolerance)
                        return ConfirmationBarIndex(bars, p.Date);
                }
            }
            for (var i = lo; i <= conf; i++)
            {
                var price = anchor.IsUp ? bars[i].High : bars[i].Low;
                if (Math.Abs(price - anchor.NewValue) <= tolerance) return i;
            }
            return conf;
        }

        /// <summary>
        /// Versão diária (barras 1440, comparação por dia): retrocede do dia
        /// de confirmação ao dia do PRIMEIRO toque no extremo.
        /// <paramref name="lowerDay"/> limita a busca à perna vigente.
        /// Espelha <c>resolveDailyAnchorDay</c>.
        /// </summary>
        public static DateTime ResolveDailyAnchorDay(
            IReadOnlyList<BarStorageItem> dailyBars,
            IReadOnlyList<StructureStorageItem> history,
            string symbol,
            StructureChange anchor,
            DateTime? lowerDay = null)
        {
            static DateTime DayOnly(DateTime d) => d.Date;
            var confirmationDay = DayOnly(anchor.Date);
            var tolerance = Defaults.GetTickSize(symbol) / 2;

            var prints = history
                .Where(s => s.Symbol == symbol && s.TimeFrame == DailyTimeFrame)
                .OrderBy(s => s.Date)
                .ToList();
            if (prints.Count > 0)
            {
                var loDay = lowerDay is { } ld ? DayOnly(ld) : (DateTime?)null;
                foreach (var p in prints)
                {
                    var d = DayOnly(p.Date);
                    if (d > confirmationDay) break;
                    if (loDay is { } l && d < l) continue;
                    var aux = anchor.IsUp ? p.UpAuxBorder : p.DownAuxBorder;
                    if (Math.Abs(aux - anchor.NewValue) <= tolerance) return d;
                }
            }
            if (dailyBars.Count == 0) return confirmationDay;

            var sorted = dailyBars.OrderBy(b => b.Date).ToList();
            var confIdx = 0;
            for (var i = sorted.Count - 1; i >= 0; i--)
            {
                if (DayOnly(sorted[i].Date) <= confirmationDay)
                {
                    confIdx = i;
                    break;
                }
            }
            var loIdx = 0;
            if (lowerDay is { } lower)
            {
                var loDay = DayOnly(lower);
                for (var i = confIdx; i >= 0; i--)
                {
                    if (DayOnly(sorted[i].Date) < loDay)
                    {
                        loIdx = Math.Clamp(i + 1, 0, confIdx);
                        break;
                    }
                }
            }
            for (var i = loIdx; i <= confIdx; i++)
            {
                var price = anchor.IsUp ? sorted[i].High : sorted[i].Low;
                if (Math.Abs(price - anchor.NewValue) <= tolerance)
                    return DayOnly(sorted[i].Date);
            }
            return confirmationDay;
        }

        /// <summary>
        /// Resolve a janela intraday do automode para o timeframe informado.
        /// <paramref name="bars"/> = barras do timeframe (fechadas, ordenadas
        /// por data). Retorna null quando não há barras. Espelha
        /// <c>_applyStructureAutoFilter</c> (start=E incluso, end=bars.Count).
        /// </summary>
        public static IntradayWindowResult? ResolveIntraday(
            IReadOnlyList<BarStorageItem> bars,
            IReadOnlyList<StructureStorageItem> structures,
            string symbol,
            int timeFrame,
            double tickSize)
        {
            if (bars.Count == 0) return null;
            var end = bars.Count;

            var changes = BuildChanges(structures, symbol, timeFrame);
            if (changes.Count == 0) return Full(bars);
            var (anchor, anchorPos) = PickAnchor(changes);
            if (anchor is null) return Full(bars);

            var confirmation = ConfirmationBarIndex(bars, anchor.Value.Date);
            var resetDate = OppositeResetDate(changes, anchorPos);
            var lowerBound = 0;
            if (resetDate is { } rd) lowerBound = ConfirmationBarIndex(bars, rd);

            var start = Math.Clamp(
                ResolveExtremeBarIndex(bars, structures, symbol, timeFrame,
                    anchor.Value, confirmation, lowerBound, tickSize / 2),
                0, end - 1);
            return new IntradayWindowResult(start, end, bars[start].Date, bars[end - 1].Date);
        }

        /// <summary>
        /// Resolve a janela diária do automode 1440. <paramref name="now"/> =
        /// dia de referência (default hoje). Espelha
        /// <c>resolveDailyAutoWindow</c>.
        /// </summary>
        public static (DateTime From, DateTime To) ResolveDaily(
            IReadOnlyList<BarStorageItem> dailyBars,
            IReadOnlyList<StructureStorageItem> history,
            string symbol,
            DateTime? now = null)
        {
            var to = (now ?? DateTime.Now).Date;
            var from = to.AddDays(-DailyFallbackDays);

            var daily = history
                .Where(s => s.Symbol == symbol && s.TimeFrame == DailyTimeFrame)
                .OrderBy(s => s.Date)
                .ToList();
            if (daily.Count >= 2)
            {
                var changes = new List<StructureChange>();
                for (var i = 1; i < daily.Count; i++)
                {
                    var prev = daily[i - 1];
                    var curr = daily[i];
                    if (curr.UpBorder != prev.UpBorder)
                        changes.Add(new StructureChange(curr.Date, true, prev.UpBorder, curr.UpBorder));
                    if (curr.DownBorder != prev.DownBorder)
                        changes.Add(new StructureChange(curr.Date, false, prev.DownBorder, curr.DownBorder));
                }
                if (changes.Count > 0)
                {
                    // Desc por data (estável): preserva up-antes-de-down em
                    // datas iguais — paridade com o sort do Dart (estável para
                    // listas pequenas).
                    var changesDesc = changes.OrderByDescending(c => c.Date).ToList();
                    var (anchor, anchorPos) = PickAnchor(changesDesc);
                    var bars = dailyBars.OrderBy(b => b.Date).ToList();
                    if (anchor is { } a)
                    {
                        var resetDate = OppositeResetDate(changesDesc, anchorPos);
                        var lowerDay = resetDate is { } rd ? rd.Date : (DateTime?)null;
                        from = ResolveDailyAnchorDay(bars, history, symbol, a, lowerDay);
                    }
                    else
                    {
                        from = ResolveDailyAnchorDay(bars, history, symbol, changesDesc[^1]);
                    }
                }
            }
            if (from > to) from = to.AddDays(-DailyFallbackDays);
            return (from, to);
        }

        private static (StructureChange? Anchor, int AnchorPos) PickAnchor(
            IReadOnlyList<StructureChange> changesDesc)
        {
            if (changesDesc.Count == 0) return (null, -1);
            var last = changesDesc[0];
            for (var k = 0; k < changesDesc.Count; k++)
            {
                var c = changesDesc[k];
                if (c.IsUp != last.IsUp && c.IsUpMove != last.IsUpMove)
                    return (c, k);
            }
            return (null, -1);
        }

        private static IntradayWindowResult Full(IReadOnlyList<BarStorageItem> bars) =>
            new(0, bars.Count, bars[0].Date, bars[^1].Date);
    }
}
