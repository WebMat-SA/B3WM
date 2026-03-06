using B3WM.Shared.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;
using Vizor.ECharts;

namespace B3WM.Client.Services
{
    public class VolumeHelper : IDisposable
    {
        public event EventHandler<int>? OnQueueCount;
        public event EventHandler<List<VolumeLevel>>? OnVolumeUpdate;
        public event EventHandler<string>? OnQueueTime;

        private readonly ConcurrentQueue<Ticks2[]> _queue = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly ConcurrentDictionary<double, VolumeLevel> _volumes = new();

        private int _queueCount { get; set; }
        private string? _queueTime { get; set; }

        private PeriodicTimer? _timer;

        public void Init(int throtlingms = 200)
        {
            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));
            _ = RunLoop();
        }

        private async Task RunLoop()
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                OnVolumeUpdate?.Invoke(this, GetSnapshot());
                OnQueueCount?.Invoke(this, _queueCount);
                if (!string.IsNullOrEmpty(_queueTime)) OnQueueTime?.Invoke(this, _queueTime);
            }
        }

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

        public void Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return ;

            _queue.Enqueue(ticks);
            _ = ProcessQueueAsync();
        }

        private Task ProcessQueueAsync()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                long tickProcessingMs = 0;

                while (_queue.TryDequeue(out var ticks))
                {
                    _queueCount = ticks.Length;
                    _queueTime = ticks.Last().Time.ToString("HH:mm:ss");
                    var swTicks = Stopwatch.StartNew();
                    foreach (var t in ticks)
                    {
                        ProcessTick(t);
                    }
                    swTicks.Stop();
                    tickProcessingMs += swTicks.ElapsedMilliseconds;
                }

            }
            finally
            {
                sw.Stop();
                HelperPerformanceConfig.Log(
                        nameof(VolumeHelper),
                        "ProcessQueueAsync",
                        sw.ElapsedMilliseconds,
                        $"priceLevels={_volumes.Count}");

            }

            return Task.CompletedTask;
        }

        private void ProcessTick(Ticks2 t)
        {
            bool? isBuyAggression = null;

            if (t.Starter == Ticks2.ActionType.Buy)
                isBuyAggression = true;
            else if (t.Starter == Ticks2.ActionType.Sale)
                isBuyAggression = false;

            _volumes.AddOrUpdate(
                t.Value,
                price =>
                {
                    var level = new VolumeLevel
                    {
                        Price = price,
                        Total = t.Volume
                    };

                    if (isBuyAggression != null)
                    {
                        if (isBuyAggression.Value)
                            level.BuyVolume = t.Volume;
                        else
                            level.SellVolume = t.Volume;
                    }

                    return level;
                },
                (price, existing) =>
                {
                    existing.Total += t.Volume;

                    if (isBuyAggression != null)
                    {
                        if (isBuyAggression.Value)
                            existing.BuyVolume += t.Volume;
                        else
                            existing.SellVolume += t.Volume;
                    }

                    return existing;
                }
            );
        }

        public void Reset()
        {
            _volumes.Clear();
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
