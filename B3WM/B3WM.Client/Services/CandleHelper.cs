using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace B3WM.Client.Services
{
    public class CandleHelper : IDisposable
    {
        private const int YieldEveryTicks = 256;
        private readonly Action<IEnumerable<Bars>>? OnClosedBars;
        private readonly int TimeFrameMinutes;

        private readonly ConcurrentQueue<byte[]> _queue = new();
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
            OnClosedBars = onClosedBars;
            TimeFrameMinutes = timeFrameMinutes;
        }

        // 🔥 UI pode consultar quando quiser
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

        public Task Enqueue(byte[] data)
        {
            if (data == null) return Task.CompletedTask;

            _queue.Enqueue(data);
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

                    while (_queue.TryDequeue(out var item))
                    {
                        chunks++;
                        Interlocked.Decrement(ref _pendingChunks);

                        swParse.Restart();
                        var helper = new DataHelper(item);
                        var ticks = helper.TimesAndTrades();
                        swParse.Stop();
                        parseMs += swParse.ElapsedMilliseconds;
                        tickCount += ticks.Count;

                        var swTicks = Stopwatch.StartNew();
                        foreach (var t in ticks)
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
            var candleStart = t.Time.GetCandleStart(TimeFrameMinutes);

            Bars? closedBar = null;

            lock (_lock)
            {
                if (_currentBar == null)
                {
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume);
                    _currentBarVersion++;
                    return;
                }

                if (_currentBar.Date != candleStart)
                {
                    // 🔥 BAR FECHOU
                    closedBar = _currentBar;

                    // cria nova
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume);
                    _currentBarVersion++;
                }
                else
                {
                    UpdateBar(_currentBar, t.Value, t.Volume);
                    _currentBarVersion++;
                }
            }

            // 🔥 Evento imediato fora do lock
            if (closedBar != null)
            {
                var sw = Stopwatch.StartNew();
                OnClosedBars?.Invoke(new[] { CloneBar(closedBar) });
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
