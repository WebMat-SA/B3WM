using B3WM.Shared.Entity;
using System.Collections.Concurrent;

namespace B3WM.Client.Services
{
    public class VolumeHelper : IDisposable
    {
        private readonly Action<IEnumerable<VolumeLevel>>? OnVolumeSnapshot;

        private readonly ConcurrentQueue<byte[]> queue = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly ConcurrentDictionary<double, VolumeLevel> _volumes = new();

        private readonly object _lock = new();

        private int _isProcessing = 0;

        private readonly int _throttleMs;
        private long _lastDispatch;

        public VolumeHelper(Action<IEnumerable<VolumeLevel>> onVolumeSnapshot, int throttleMs = 200)
        {
            OnVolumeSnapshot = onVolumeSnapshot;
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
                        if (item == null) continue;

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
            bool? isBuyAggression = null;

            if (t.Starter == Ticks2.ActionType.Buy)
                isBuyAggression = true;
            else if (t.Starter == Ticks2.ActionType.Sale)
                isBuyAggression = false;

            if (isBuyAggression == null)
                return;

            lock (_lock)
            {
                var level = _volumes.GetOrAdd(t.Value, price =>
                    new VolumeLevel { Price = price });

                level.Total += t.Volume;

                if (isBuyAggression.Value)
                    level.BuyVolume += t.Volume;
                else
                    level.SellVolume += t.Volume;
            }

            TryDispatchSnapshot();
        }

        private void TryDispatchSnapshot()
        {
            var now = Environment.TickCount64;

            if (now - _lastDispatch < _throttleMs)
                return;

            _lastDispatch = now;

            List<VolumeLevel> snapshot;

            lock (_lock)
            {
                snapshot = _volumes.Values
                    .OrderBy(v => v.Price)
                    .Select(v => new VolumeLevel
                    {
                        Price = v.Price,
                        Total = v.Total,
                        BuyVolume = v.BuyVolume,
                        SellVolume = v.SellVolume
                    })
                    .ToList();
            }

            OnVolumeSnapshot?.Invoke(snapshot);
        }

        public void Reset()
        {
            lock (_lock)
            {
                _volumes.Clear();
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
        }
    }
}
