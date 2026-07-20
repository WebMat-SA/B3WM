using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services.Core
{
    public class IndicatorService : IHostedService
    {
        private readonly IHubContext<DataHub, IDataHubClient> _hub;
        private readonly IEnumerable<Indicators.IIndicator> _indicators;
        private readonly IEnumerable<CandleService> _candleServices;
        private readonly Dictionary<string, List<BarStorageItem>> _history = new();

        public IndicatorService(
            IHubContext<DataHub, IDataHubClient> hub,
            IEnumerable<Indicators.IIndicator> indicators,
            IEnumerable<CandleService> candleServices)
        {
            _hub = hub;
            _indicators = indicators;
            _candleServices = candleServices;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var cs in _candleServices)
                cs.OnUpdate += OnCandleUpdate;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var cs in _candleServices)
                cs.OnUpdate -= OnCandleUpdate;
            return Task.CompletedTask;
        }

        private async Task OnCandleUpdate(BarStorageItem bar)
        {
            var key = $"{bar.Symbol}_{bar.TimeFrame}";
            if (!_history.ContainsKey(key))
                _history[key] = new();

            _history[key].Add(bar);
            if (_history[key].Count > 500)
                _history[key].RemoveRange(0, _history[key].Count - 500);

            foreach (var ind in _indicators.Where(i => i.Enabled))
            {
                if (_history[key].Count < ind.NeedsBarCount)
                    continue;

                var skip = _history[key].Count - ind.NeedsBarCount;
                var hist = _history[key].Skip(skip).ToList();
                var results = ind.Evaluate(bar, hist);

                foreach (var r in results)
                {
                    await _hub.Clients.Group(bar.Symbol).ReceiveOnIndicatorValue(
                        new IndicatorValue
                        {
                            Time = bar.Date,
                            Symbol = bar.Symbol,
                            IndicatorName = ind.Name,
                            Key = r.Key,
                            Value = r.Value,
                            PlotType = r.PlotType
                        });
                }
            }
        }
    }
}
