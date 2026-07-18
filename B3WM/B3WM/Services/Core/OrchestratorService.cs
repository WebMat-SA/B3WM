using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services.Core
{
    public class OrchestratorService : IDisposable, ISymbolable
    {
        private readonly IEnumerable<CandleService> candleService;
        private readonly IEnumerable<BubbleService> bubbleService;
        private readonly IEnumerable<VolumeService> volumeService;
        private readonly IEnumerable<StructureService> structureService;
        private readonly IEnumerable<AdjustmentForecastService> adjustmentForecastService;
        public string Symbol { get; }

        public OrchestratorService(string Symbol, IHubContext<DataHub, IDataHubClient> hubContext,IEnumerable<CandleService> candleService, IEnumerable<BubbleService> bubbleService, IEnumerable<VolumeService> volumeService, IEnumerable<StructureService> structureService, IEnumerable<AdjustmentForecastService> adjustmentForecastService)
        {
            this.Symbol = Symbol;

            this.candleService = candleService.Where(q=>q.Symbol == Symbol);
            this.bubbleService = bubbleService.Where(q=>q.Symbol == Symbol);
            this.volumeService = volumeService.Where(q=>q.Symbol == Symbol);
            this.structureService = structureService.Where(q=>q.Symbol == Symbol);
            this.adjustmentForecastService = adjustmentForecastService.Where(q=>q.Symbol == Symbol);

            SubscribeAll(this.candleService, OnCandleUpdate);
        }

        public Task Enqueue(Ticks2[] ticks)
        {

            // enqueue to all processors using shared helper
            EnqueueAll(candleService, ticks);
            EnqueueAll(bubbleService, ticks);
            EnqueueAll(volumeService, ticks);
            EnqueueAll(adjustmentForecastService, ticks);

            return Task.CompletedTask;
        }

        private async Task OnCandleUpdate(BarStorageItem bar)
        {
            try
            {
                #region Volume in Candles
                //se quiser fazer o input de outros serviços no finalizar de candle (como por exemplo indicadores, vwap, etc)
                var volumeSnapshot = volumeService.FirstOrDefault()?.GetSnapshot();

                if (volumeSnapshot != null)
                    bar.VolumeLevel = volumeSnapshot.Volumes;

                #endregion

                #region Forecast in Candles
                var forecastSnapshot = adjustmentForecastService.FirstOrDefault()?.GetSnapshot();

                if (forecastSnapshot != null && forecastSnapshot.Vwap > 0)
                    bar.ForecastPrice = forecastSnapshot.Vwap;

                #endregion

                #region Structure by Candles
                var structureService = this.structureService.FirstOrDefault(q=>q.TimeFrame == bar.TimeFrame);

                if (structureService != null)
                {
                    await structureService.Calculate(bar);
                }

                #endregion

                Console.WriteLine(
                    $"Bar atualizado: {bar} ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OrchestratorService.OnCandleUpdate error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            UnsubscribeAll(candleService, OnCandleUpdate);
        }

        private void SubscribeAll<T>(IEnumerable<IProcessor<Ticks2, T>> services, Func<T, Task> handler)
        {
            if (services == null) return;

            foreach (var service in services)
            {
                service.OnUpdate += handler;
            }
        }

        private void UnsubscribeAll<T>(IEnumerable<IProcessor<Ticks2, T>> services, Func<T, Task> handler)
        {
            if (services == null) return;

            foreach (var service in services)
            {
                service.OnUpdate -= handler;
            }
        }

        private void EnqueueAll<T>(IEnumerable<IProcessor<Ticks2, T>> services, Ticks2[] ticks)
        {
            if (services == null) return;

            foreach (var service in services)
            {
                service.Enqueue(ticks);
            }
        }
    }

    public interface IProcessor<T,U>
    {
        event Func<U, Task>? OnUpdate;
        void Enqueue(T[] items);
        U GetSnapshot();
    }

    public interface ISymbolable
    {
        string Symbol { get; }
    }
}
