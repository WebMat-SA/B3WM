using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Threading.Channels;

namespace B3WM.Services.Core
{
    public class AdjustmentForecastService : DataKeeperService<AdjustmentForecastItem>, IProcessor<Ticks2, AdjustmentForecastItem>, ISymbolable
    {
        public string Symbol { get; }

        private readonly IHubContext<DataHub, IDataHubClient> hubContext;
        private readonly ILogger<AdjustmentForecastService> _logger;

        private readonly Channel<Ticks2[]> _channel =
            Channel.CreateUnbounded<Ticks2[]>();

        private readonly PeriodicTimer _saveTimer = new(TimeSpan.FromMinutes(1));

        public event Func<AdjustmentForecastItem, Task>? OnUpdate;

        private double _runningSumPv;
        private long _runningSumV;
        private DateTime _currentDay;
        private AdjustmentForecastItem _currentForecast = new();

        public override string Path => $"{Symbol}_{nameof(AdjustmentForecastService)}_{DateTime.Now:yyyy-MM-dd}.json";

        public AdjustmentForecastService(string symbol, IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider, ILogger<AdjustmentForecastService> logger)
            : base(serviceProvider)
        {
            Symbol = symbol;
            this.hubContext = hubContext;
            _logger = logger;

            _ = Task.Run(ProcessLoop);
            _ = Task.Run(DataKeeperLoop);
        }

        public void Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return;
            _channel.Writer.TryWrite(ticks);
        }

        public AdjustmentForecastItem GetSnapshot() => _currentForecast;

        private async Task ProcessLoop()
        {
            await LoadFromLastAvailableDay();

            await foreach (var ticks in _channel.Reader.ReadAllAsync())
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    IList<Ticks2> sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();
                    var lastTick = sortedTicks.Last();
                    var tickDay = lastTick.Time.Date;

                    if (_currentDay == default)
                    {
                        _currentDay = tickDay;
                    }
                    else if (tickDay != _currentDay)
                    {
                        _runningSumPv = 0;
                        _runningSumV = 0;
                        _currentDay = tickDay;
                    }

                    foreach (var t in sortedTicks)
                    {
                        if (t.Starter == Ticks2.ActionType.Auction)
                            continue;

                        _runningSumPv += t.Value * t.Volume;
                        _runningSumV += t.Volume;
                    }

                    if (_runningSumV > 0)
                    {
                        var vwap = _runningSumPv / _runningSumV;
                        _currentForecast = new AdjustmentForecastItem
                        {
                            Vwap = vwap,
                            Time = lastTick.Time,
                            Symbol = lastTick.Symbol,
                            SumPv = _runningSumPv,
                            SumV = _runningSumV,
                            Day = _currentDay
                        };

                        DataKeep = _currentForecast;

                        if (hubContext != null)
                        {
                            await hubContext.Clients.Group(Symbol).ReceiveOnForecast(_currentForecast);
                        }

                        if (OnUpdate != null)
                        {
                            await OnUpdate.Invoke(_currentForecast);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AdjustmentForecastService.ProcessLoop error");
                }
            }
        }

        private async Task LoadFromLastAvailableDay()
        {
            for (int i = 0; i < 30; i++)
            {
                var date = DateTime.Now.AddDays(-i);
                var path = $"{Symbol}_{nameof(AdjustmentForecastService)}_{date:yyyy-MM-dd}.json";
                try
                {
                    var data = await GetDataAsync(path);
                    if (data != null && data.SumV > 0 && data.Time != default)
                    {
                        _currentForecast = data;
                        _runningSumPv = data.SumPv;
                        _runningSumV = data.SumV;
                        _currentDay = data.Day != default ? data.Day : data.Time.Date;
                        _logger.LogInformation("Loaded forecast from {Path}: VWAP={Vwap}, Day={Day}", path, data.Vwap, _currentDay);
                        return;
                    }
                }
                catch
                {
                    // arquivo não existe ou erro de leitura — tenta próximo dia
                }
            }

            _logger.LogWarning("No previous forecast data found for {Symbol}", Symbol);
        }

        private async Task DataKeeperLoop()
        {
            await LoadFromLastAvailableDay();

            while (await _saveTimer.WaitForNextTickAsync())
            {
                try
                {
                    if (_currentForecast != null && _currentForecast.SumV > 0)
                    {
                        await SetDataAsync(_currentForecast);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AdjustmentForecastService.DataKeeperLoop error");
                }
            }
        }
    }
}
