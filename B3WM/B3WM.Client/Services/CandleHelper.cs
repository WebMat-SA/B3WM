using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace B3WM.Client.Services
{
    public class CandleHelper : IDisposable
    {
        private const int YieldEveryTicks = 256;
        private readonly Action<IEnumerable<Bars>>? _onClosedBars;
        private readonly int _timeFrameMinutes;

        private readonly ConcurrentQueue<IReadOnlyList<Ticks2>> _queue = new();
        private int _isProcessing = 0;
        private int _pendingChunks = 0;
        private int _batchSequence = 0;
        private readonly CancellationTokenSource _cts = new();

        private readonly object _lock = new();
        private Bars? _currentBar;
        private int _currentBarVersion;
        private int _closedBarsEmittedInBatch;
        private long _closedBarsCallbackMsInBatch;

        public CandleHelper(
            Action<IEnumerable<Bars>> onClosedBars,
            int timeFrameMinutes = 5)
        {
            _onClosedBars = onClosedBars;
            _timeFrameMinutes = timeFrameMinutes;
        }

        public Bars? GetCurrentBarSnapshot()
        {
            lock (_lock)
            {
                if (_currentBar == null)
                    return null;

                return CloneBar(_currentBar);
            }
        }

        public int GetCurrentBarVersion()
        {
            lock (_lock)
            {
                return _currentBarVersion;
            }
        }

        public Task Enqueue(IReadOnlyList<Ticks2> ticks)
        {
            if (ticks == null || ticks.Count == 0) return Task.CompletedTask;

            _queue.Enqueue(ticks);
            Interlocked.Increment(ref _pendingChunks);

            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
            {
                _ = Task.Run(ProcessQueueAsync, _cts.Token);
            }

            return Task.CompletedTask;
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                var token = _cts.Token;

                while (!token.IsCancellationRequested)
                {
                    var sw = Stopwatch.StartNew();
                    var swParse = Stopwatch.StartNew();
                    int chunks = 0, tickCount = 0;
                    int processedTicksSinceYield = 0;
                    long parseMs = 0;
                    long tickProcessingMs = 0;
                    _closedBarsEmittedInBatch = 0;
                    _closedBarsCallbackMsInBatch = 0;

                    while (_queue.TryDequeue(out var ticks))
                    {
                        Interlocked.Decrement(ref _pendingChunks);

                        swParse.Restart();
                        var sortedTicks = ticks.OrderBy(x => x.Time).ToList();
                        swParse.Stop();
                        parseMs += swParse.ElapsedMilliseconds;
                        tickCount += ticks.Count;

                        var swTicks = Stopwatch.StartNew();
                        foreach (var t in sortedTicks)
                        {
                            ProcessTick(t);
                            processedTicksSinceYield++;
                            if ((processedTicksSinceYield & (YieldEveryTicks - 1)) == 0)
                                await Task.Yield(); // libera o loop WASM sem custo por tick
                        }
                        swTicks.Stop();
                        tickProcessingMs += swTicks.ElapsedMilliseconds;
                    }

                    sw.Stop();
                    if (chunks > 0)
                    {
                        var seq = Interlocked.Increment(ref _batchSequence);
                        var pending = Volatile.Read(ref _pendingChunks);
                        HelperPerformanceConfig.LogSampled(
                            nameof(CandleHelper),
                            "ProcessQueueAsync",
                            sw.ElapsedMilliseconds,
                            seq,
                            $"chunks={chunks} ticks={tickCount} parseMs={parseMs} tickMs={tickProcessingMs} closedBars={_closedBarsEmittedInBatch} closeCbMs={_closedBarsCallbackMsInBatch} pending={pending}");
                    }

                    Interlocked.Exchange(ref _isProcessing, 0);

                    if (!_queue.IsEmpty &&
                        Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                        continue;

                    break;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        private void ProcessTick(Ticks2 t)
        {
            var candleStart = t.Time.GetCandleStart(_timeFrameMinutes);

            Bars? barToEmit = null;

            lock (_lock)
            {
                if (_currentBar == null)
                {
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume);
                    _currentBarVersion++;
                    return;
                }

                if (candleStart > _currentBar.Date)
                {
                    // Só fechar quando o tick é de um período posterior (evita fechar por duplicata ou ordem inversa).
                    barToEmit = CloneBar(_currentBar);
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume);
                    _currentBarVersion++;
                }
                else if (candleStart == _currentBar.Date)
                {
                    UpdateBar(_currentBar, t.Value, t.Volume);
                    _currentBarVersion++;
                }
                // candleStart < _currentBar.Date: tick do passado (ordem inversa/atrasado), ignorar
            }

            // Emitir fora do lock; usamos só a cópia já feita.
            if (barToEmit != null)
            {
                var sw = Stopwatch.StartNew();
                _onClosedBars?.Invoke(new[] { barToEmit });
                sw.Stop();
                _closedBarsEmittedInBatch++;
                _closedBarsCallbackMsInBatch += sw.ElapsedMilliseconds;
            }
        }

        private Bars CloneBar(Bars bar)
        {
            return new Bars
            {
                Date = bar.Date,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume
            };
        }

        private Bars CreateNewBar(DateTime start, double price, long volume)
        {
            return new Bars
            {
                Date = start,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = volume
            };
        }

        private void UpdateBar(Bars bar, double price, long volume)
        {
            if (price > bar.High) bar.High = price;
            if (price < bar.Low) bar.Low = price;

            bar.Close = price;
            bar.Volume += volume;
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
