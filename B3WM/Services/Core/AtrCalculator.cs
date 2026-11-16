using B3WM.Shared.Models;

namespace B3WM.Services.Core
{
    /// <summary>
    /// ATR puro (sem I/O), no mesmo espírito do cálculo usado pela Jev em
    /// <c>JevContextFeatures</c>: true range candle a candle e média simples
    /// dos últimos N períodos. Todos os valores monetários estão em pontos;
    /// a conversão para ticks fica a cargo do chamador.
    /// </summary>
    public static class AtrCalculator
    {
        /// <summary>
        /// True range do candle: máxima entre high-low e os gaps contra o
        /// fechamento anterior. O primeiro candle usa apenas high-low.
        /// </summary>
        public static double TrueRange(BarStorageItem current, BarStorageItem? previous)
        {
            var range = current.High - current.Low;
            if (previous == null)
                return range;

            return Math.Max(range, Math.Max(
                Math.Abs(current.High - previous.Close),
                Math.Abs(current.Low - previous.Close)));
        }

        /// <summary>
        /// ATR dos últimos <paramref name="period"/> candles. Usa os candles
        /// disponíveis quando a série é menor que o período.
        /// </summary>
        public static double AverageTrueRange(IReadOnlyList<BarStorageItem> candles, int period)
        {
            if (candles == null || candles.Count == 0)
                return 0;

            var p = Math.Max(1, period);
            var start = Math.Max(0, candles.Count - p);
            var sum = 0.0;
            var count = 0;

            for (var i = start; i < candles.Count; i++)
            {
                var previous = i > 0 ? candles[i - 1] : null;
                sum += TrueRange(candles[i], previous);
                count++;
            }

            return count > 0 ? sum / count : 0;
        }

        /// <summary>
        /// Converte ATR (em pontos) em range de estrutura aplicando
        /// multiplicador e limites em ticks. Retorna 0 quando não há base
        /// válida (o chamador decide o fallback).
        /// </summary>
        public static double ClampRangePoints(
            double atrPoints,
            double multiplier,
            double tickSize,
            double floorTicks,
            double capTicks)
        {
            if (atrPoints <= 0 || multiplier <= 0 || tickSize <= 0)
                return 0;

            var floor = Math.Max(0, floorTicks) * tickSize;
            var cap = Math.Max(0, capTicks) * tickSize;
            if (cap > 0 && cap < floor)
                cap = floor;

            var range = atrPoints * multiplier;
            if (floor > 0)
                range = Math.Max(floor, range);
            if (cap > 0)
                range = Math.Min(cap, range);

            return range;
        }
    }

    /// <summary>
    /// ATR incremental com janela fixa. Mantém o fechamento anterior e os
    /// true ranges recentes; cada <see cref="Add"/> é O(1) amortizado.
    /// </summary>
    public sealed class AtrState
    {
        private readonly int _period;
        private readonly Queue<double> _trueRanges = new();
        private BarStorageItem? _previous;

        public AtrState(int period)
        {
            _period = Math.Max(1, period);
        }

        /// <summary>ATR atual em pontos (0 antes do primeiro candle).</summary>
        public double Value { get; private set; }

        /// <summary>Quantidade de true ranges na janela atual.</summary>
        public int Samples => _trueRanges.Count;

        public double ValueInTicks(double tickSize) =>
            tickSize > 0 ? Value / tickSize : 0;

        /// <summary>Adiciona um candle fechado e devolve o ATR atualizado.</summary>
        public double Add(BarStorageItem bar)
        {
            var trueRange = AtrCalculator.TrueRange(bar, _previous);
            _previous = bar;

            _trueRanges.Enqueue(trueRange);
            while (_trueRanges.Count > _period)
                _trueRanges.Dequeue();

            Value = _trueRanges.Count > 0
                ? _trueRanges.Average()
                : 0;

            return Value;
        }

        public void Warmup(IEnumerable<BarStorageItem> bars)
        {
            foreach (var bar in bars)
                Add(bar);
        }

        public void Reset()
        {
            _trueRanges.Clear();
            _previous = null;
            Value = 0;
        }
    }
}
