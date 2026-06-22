using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using System.Timers;

namespace B3WM.Services.Core
{
    public class VolumeService : DataKeeperService<VolumeLevelStorageItem>, IProcessor<Ticks2, VolumeLevelStorageItem>, ISymbolable
    {
        public string Symbol { get; }

        private readonly IHubContext<DataHub, IDataHubClient> hubContext;

        PeriodicTimer _timer { get; set; }

        private readonly Channel<Ticks2[]> _channel =
            Channel.CreateUnbounded<Ticks2[]>();

        public event Func<VolumeLevelStorageItem, Task>? OnUpdate;

        private readonly ConcurrentDictionary<double, VolumeLevel> _volumes = new();

        private VolumeLevelStorageItem _currentSnapshot = new VolumeLevelStorageItem
        {
            Date = DateTime.Now,
            Symbol = string.Empty,
            TimeFrame = 0,
            Volumes = new List<VolumeLevel>()
        };

        private bool _intraDayAdded { get; set; } = false;

        public override string Path => $"{Symbol}_{nameof(VolumeService)}_{DateTime.Now:yyyy-MM-dd}.json";

        public VolumeService(string symbol, IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider)
            :base(serviceProvider)
        {
            Symbol = symbol;
            this.hubContext = hubContext;
            _timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            _ = Task.Run(ProcessLoop);
            _ = Task.Run(DataKeeperLoop);
        }

        public void Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return;

            _channel.Writer.TryWrite(ticks);
        }

        // interface implementation
        public VolumeLevelStorageItem GetSnapshot() => BuildSnapshot();

        private VolumeLevelStorageItem BuildSnapshot()
        {
            _currentSnapshot.Volumes = _volumes.Values
                .OrderBy(v => v.Price)
                .Select(v => new VolumeLevel
                {
                    Price = v.Price,
                    Total = v.Total,
                    BuyVolume = v.BuyVolume,
                    SellVolume = v.SellVolume
                })
                .ToList();

            return _currentSnapshot;
        }

        private async Task ProcessLoop()
        {
            await foreach (var ticks in _channel.Reader.ReadAllAsync())
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    IList<Ticks2> sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();

                    foreach (var t in sortedTicks)
                    {
                        ProcessTick(t);
                    }

                    var lastTick = sortedTicks.LastOrDefault();
                    if (lastTick != null)
                    {
                        _currentSnapshot.Symbol = lastTick.Symbol;
                        _currentSnapshot.TimeFrame = 0; // definir se necessário
                        _currentSnapshot.Date = lastTick.Time;
                    }

                    sw.Stop();

                    if (OnUpdate != null)
                    {
                        try
                        {
                            var snapShot = BuildSnapshot();
                            await OnUpdate.Invoke(snapShot);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"OnUpdate error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"VolumeService.ProcessLoop error: {ex.Message}");
                }
            }
        }

        private void ProcessTick(Ticks2 t)
        {
            bool? isBuyAggression = null;

            // ignore auctions
            if (t.Starter == Ticks2.ActionType.Auction)
                return;

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

        public void AddIntradayVolume(List<VolumeLevel> volumes)
        {
            if (_intraDayAdded) return;

            foreach (var item in volumes)
            {
                _volumes.AddOrUpdate(
                    item.Price,
                    price => item,
                    (price, existing) =>
                    {
                        existing.Total += item.Total;
                        existing.SellVolume += item.SellVolume;
                        existing.BuyVolume += item.BuyVolume;

                        return existing;
                    }
                );
            }

            _intraDayAdded = true;
        }

        private async Task DataKeeperLoop()
        {
            // se houver arquivo no sistema com a especificação desse serviço, já carrega na memória para evitar perda de dados.
            await LoadAsync();

            AddIntradayVolume(DataKeep.Volumes);

            while (await _timer.WaitForNextTickAsync())
            {
                try
                {
                    await SetDataAsync(BuildSnapshot());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"VolumeService.DataKeeperLoop error: {ex.Message}");
                }
            }
        }
    }
}
