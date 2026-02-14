using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using System.Collections.Concurrent;

namespace B3WM.Client.Services
{
    public class CandleHelper : IDisposable
    {
        private readonly Action<IEnumerable<Bars>>? OnClosedBars;
        private readonly int TimeFrameMinutes;

        private readonly ConcurrentQueue<byte[]> _queue = new();
        private int _isProcessing = 0;
        private readonly CancellationTokenSource _cts = new();

        private readonly object _lock = new();
        private Bars? _currentBar;

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

        public Task Enqueue(byte[] data)
        {
            if (data == null) return Task.CompletedTask;

            _queue.Enqueue(data);

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
                    while (_queue.TryDequeue(out var item))
                    {
                        var helper = new DataHelper(item);
                        var ticks = helper.TimesAndTrades();

                        foreach (var t in ticks)
                        {
                            ProcessTick(t);
                            await Task.Yield(); // libera o loop WASM
                        }
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
                    return;
                }

                if (_currentBar.Date != candleStart)
                {
                    // 🔥 BAR FECHOU
                    closedBar = _currentBar;

                    // cria nova
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume);
                }
                else
                {
                    UpdateBar(_currentBar, t.Value, t.Volume);
                }
            }

            // 🔥 Evento imediato fora do lock
            if (closedBar != null)
            {
                OnClosedBars?.Invoke(new List<Bars> { CloneBar(closedBar) });
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
