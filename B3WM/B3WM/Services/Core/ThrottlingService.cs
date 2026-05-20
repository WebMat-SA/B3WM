using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services.Core
{
    public class ThrottlingService : BackgroundService, ISymbolable
    {
        public string Symbol { get; }
        private readonly IHubContext<DataHub, IDataHubClient> hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly PeriodicTimer timer;

        public ThrottlingService(string symbol, IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider, int intervalMilliseconds=250)
        {
            this.Symbol = symbol;
            this.hubContext = hubContext;
            this._serviceProvider = serviceProvider;
            this.timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMilliseconds));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var candleServices = _serviceProvider.GetServices<CandleService>().Where(c => c.Symbol == Symbol);
            var volumeService = _serviceProvider.GetServices<VolumeService>().Where(v => v.Symbol == Symbol).FirstOrDefault();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var listCandleTimeFrame = new List<BarStorageItem>();

                    foreach(var candleService in candleServices)
                    {
                        listCandleTimeFrame.Add(candleService.GetSnapshot());
                    }

                    ThrottlingData data = new ThrottlingData()
                    {
                        Candle = listCandleTimeFrame,
                        Volume = volumeService?.GetSnapshot()
                    };

                    // Perform throttling logic here
                    await hubContext.Clients.Groups(Symbol).ReceiveThrottlingData(data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
