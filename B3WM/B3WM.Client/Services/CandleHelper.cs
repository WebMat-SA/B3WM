using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using System.Collections.Concurrent;

namespace B3WM.Client.Services
{
    public class CandleHelper : IDisposable
    {
        private readonly Action<IEnumerable<Bars>>? OnClosedBars;
        private readonly Action<Bars>? OnCurrentBarUpdated;

        private readonly int TimeFrameMinutes;
        private readonly int _throttleMs;
        private long _lastDispatch;

        private ConcurrentQueue<byte[]> queue = new();
        private int _isProcessing = 0;
        private readonly CancellationTokenSource _cts = new();

        private readonly object _lock = new();
        private Bars? _currentBar;

        public CandleHelper(
            Action<IEnumerable<Bars>> onClosedBars,
            Action<Bars>? onCurrentBarUpdated = null,
            int timeFrameMinutes = 5,
            int throttleMs = 200)
        {
            OnClosedBars = onClosedBars;
            OnCurrentBarUpdated = onCurrentBarUpdated;
            TimeFrameMinutes = timeFrameMinutes;
            _throttleMs = throttleMs;
            _lastDispatch = Environment.TickCount64;
        }

        public Task Enqueue(byte[] data)
        {
            if (data == null) return Task.CompletedTask;

            queue.Enqueue(data);

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
                    while (queue.TryDequeue(out var item))
                    {
                        var helper = new DataHelper(item);
                        var ticks = helper.TimesAndTrades();

                        foreach (var t in ticks)
                        {
                            ProcessTick(t);
                            await Task.Yield();
                        }
                    }

                    Interlocked.Exchange(ref _isProcessing, 0);

                    if (!queue.IsEmpty &&
                        Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                        continue;

                    break;
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        private void ProcessTick(Ticks2 t)
        {
            var tickTime = t.Time;
            var tickPrice = t.Value;
            var tickVolume = t.Volume;

            var candleStart = tickTime.GetCandleStart(TimeFrameMinutes);

            Bars? closedBar = null;

            lock (_lock)
            {
                if (_currentBar == null)
                {
                    _currentBar = CreateNewBar(candleStart, tickPrice, tickVolume);
                    return;
                }

                if (_currentBar.Date != candleStart)
                {
                    closedBar = _currentBar;
                    _currentBar = CreateNewBar(candleStart, tickPrice, tickVolume);
                }
                else
                {
                    UpdateBar(_currentBar, tickPrice, tickVolume);
                }
            }

            // 🔥 Evento de barra fechada (imediato)
            if (closedBar != null)
            {
                OnClosedBars?.Invoke(new List<Bars> { closedBar });
            }

            // 🔥 Atualização da barra atual (throttled)
            TryDispatchCurrentBar();
        }

        private void TryDispatchCurrentBar()
        {
            if (OnCurrentBarUpdated == null)
                return;

            var now = Environment.TickCount64;

            if (now - _lastDispatch < _throttleMs)
                return;

            _lastDispatch = now;

            Bars? snapshot;

            lock (_lock)
            {
                if (_currentBar == null)
                    return;

                snapshot = CloneBar(_currentBar);
            }

            OnCurrentBarUpdated?.Invoke(snapshot);
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
                Volume = bar.Volume,
                TickVolume = bar.TickVolume,
                TypeTime = bar.TypeTime,
                CustomerID = bar.CustomerID
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
            try { _cts.Cancel(); } catch { }
        }
    }
}
