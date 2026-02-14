using B3WM.Shared.Entity;
using System.Collections.Concurrent;

namespace B3WM.Client.Services
{
    public class VolumeHelper : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> _queue = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly ConcurrentDictionary<double, VolumeLevel> _volumes = new();
        private readonly object _lock = new();

        private int _isProcessing = 0;

        public VolumeHelper()
        {
        }

        // 🔥 UI consulta quando quiser
        public List<VolumeLevel> GetSnapshot()
        {
            return _volumes.Values
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
                        if (item == null) continue;

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
            bool? isBuyAggression = null;

            if (t.Starter == Ticks2.ActionType.Buy)
                isBuyAggression = true;
            else if (t.Starter == Ticks2.ActionType.Sale)
                isBuyAggression = false;

            if (isBuyAggression == null)
                return;

            _volumes.AddOrUpdate(
                t.Value,
                price =>
                {
                    var level = new VolumeLevel
                    {
                        Price = price,
                        Total = t.Volume
                    };

                    if (isBuyAggression.Value)
                        level.BuyVolume = t.Volume;
                    else
                        level.SellVolume = t.Volume;

                    return level;
                },
                (price, existing) =>
                {
                    existing.Total += t.Volume;

                    if (isBuyAggression.Value)
                        existing.BuyVolume += t.Volume;
                    else
                        existing.SellVolume += t.Volume;

                    return existing;
                }
            );
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
            _cts.Cancel();
        }
    }
}
