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

        // Ticks brutos armazenados para permitir snapshot filtrado por intervalo de tempo.
        private readonly struct RawTick
        {
            public readonly DateTime Time;
            public readonly double Price;
            public readonly int Volume;
            public readonly bool IsBuy;
            public RawTick(DateTime time, double price, int volume, bool isBuy)
            { Time = time; Price = price; Volume = volume; IsBuy = isBuy; }
        }
        private readonly List<RawTick> _rawTicks = new();
        private readonly object _rawTicksLock = new();

        private int _volumeVersion = 0;


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
                OnQueueTime?.Invoke(this, _queueTime);
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

        /// <summary>
        /// Retorna um snapshot do volume profile agregado apenas pelos ticks
        /// cujo timestamp esteja dentro do intervalo [from, to].
        /// Se ambos forem null, equivale a <see cref="GetSnapshot()"/>.
        /// </summary>
        public List<VolumeLevel> GetSnapshot(DateTime? from, DateTime? to)
        {
            if (!from.HasValue && !to.HasValue)
                return GetSnapshot();

            RawTick[] snapshot;
            lock (_rawTicksLock)
                snapshot = _rawTicks.ToArray();

            var result = new Dictionary<double, VolumeLevel>();
            foreach (var t in snapshot)
            {
                if (from.HasValue && t.Time < from.Value) continue;
                if (to.HasValue   && t.Time > to.Value)   continue;

                if (!result.TryGetValue(t.Price, out var level))
                {
                    level = new VolumeLevel { Price = t.Price };
                    result[t.Price] = level;
                }
                level.Total += t.Volume;
                if (t.IsBuy) level.BuyVolume += t.Volume;
                else         level.SellVolume += t.Volume;
            }

            return result.Values.OrderBy(v => v.Price).ToList();
        }

        public void Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return ;

            _queue.Enqueue(ticks);
            _ = ProcessQueueAsync();
        }

        public int GetVolumeVersion()
        {
            return Volatile.Read(ref _volumeVersion);
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

            // Armazena o tick bruto para permitir recálculo por intervalo de tempo.
            lock (_rawTicksLock)
                _rawTicks.Add(new RawTick(t.Time, t.Value, t.Volume, isBuyAggression.Value));
        }

        public void Reset()
        {
            _volumes.Clear();
            lock (_rawTicksLock)
                _rawTicks.Clear();
            Interlocked.Increment(ref _volumeVersion);
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
