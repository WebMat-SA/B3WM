using B3WM.Shared.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace B3WM.Client.Services
{
    public class VolumeHelper : IDisposable
    {
        private const int YieldEveryTicks = 256;
        private readonly ConcurrentQueue<IReadOnlyList<Ticks2>> _queue = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly ConcurrentDictionary<double, VolumeLevel> _volumes = new();

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

        private int _isProcessing = 0;
        private int _volumeVersion = 0;
        private int _pendingChunks = 0;
        private int _batchSequence = 0;

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

        public int GetQueueCountSnapshot()
        {
            if (_queue == null)
                return 0;

            _ = _queue.TryGetNonEnumeratedCount(out int result);

            return result;
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

        public int GetVolumeVersion()
        {
            return Volatile.Read(ref _volumeVersion);
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                //var token = _cts.Token;

                //while (!token.IsCancellationRequested)
                //{
                    var sw = Stopwatch.StartNew();
                    int chunks = 0, tickCount = 0;
                    int processedTicksSinceYield = 0;
                    long tickProcessingMs = 0;

                    while (_queue.TryDequeue(out var ticks))
                    {
                        chunks++;
                        Interlocked.Decrement(ref _pendingChunks);
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
                            nameof(VolumeHelper),
                            "ProcessQueueAsync",
                            sw.ElapsedMilliseconds,
                            seq,
                            $"chunks={chunks} ticks={tickCount} tickMs={tickProcessingMs} priceLevels={_volumes.Count} pending={pending}");
                    }

                    Interlocked.Exchange(ref _isProcessing, 0);

                //    if (!_queue.IsEmpty &&
                //        Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                //        continue;

                //    break;
                //}
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

            // Armazena o tick bruto para permitir recálculo por intervalo de tempo.
            lock (_rawTicksLock)
                _rawTicks.Add(new RawTick(t.Time, t.Value, t.Volume, isBuyAggression.Value));

            Interlocked.Increment(ref _volumeVersion);
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
