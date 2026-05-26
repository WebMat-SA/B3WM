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
        public string Symbol { get; }

        public OrchestratorService(string Symbol, IHubContext<DataHub, IDataHubClient> hubContext,IEnumerable<CandleService> candleService, IEnumerable<BubbleService> bubbleService, IEnumerable<VolumeService> volumeService, IEnumerable<StructureService> structureService)
        {
            this.Symbol = Symbol;

            this.candleService = candleService.Where(q=>q.Symbol == Symbol);
            this.bubbleService = bubbleService.Where(q=>q.Symbol == Symbol);
            this.volumeService = volumeService.Where(q=>q.Symbol == Symbol);
            this.structureService = structureService.Where(q=>q.Symbol == Symbol);

            // subscribe using shared helper to avoid duplicated foreach loops
            SubscribeAll(this.candleService, OnCandleUpdate);
            //SubscribeAll(this.bubbleService, OnBubbleUpdate);
            //SubscribeAll(this.structureService, OnStructureUpdate);
        }

        public Task Enqueue(Ticks2[] ticks)
        {

            // enqueue to all processors using shared helper
            EnqueueAll(candleService, ticks);
            EnqueueAll(bubbleService, ticks);
            EnqueueAll(volumeService, ticks);

            return Task.CompletedTask;
        }

        private async Task OnCandleUpdate(BarStorageItem bar)
        {
            #region Volume in Candles
            //se quiser fazer o input de outros serviços no finalizar de candle (como por exemplo indicadores, vwap, etc)
            var volumeSnapshot = volumeService.FirstOrDefault()?.GetSnapshot();

            if (volumeSnapshot != null)
                bar.VolumeLevel = volumeSnapshot.Volumes;

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

            await Task.CompletedTask;
        }
        //private async Task OnBubbleUpdate(BubbleStorageItem bubble)
        //{
        //    //se quiser fazer o input de outros serviços no finalizar de bubble (como por exemplo indicadores, vwap, etc)

        //    Console.WriteLine(
        //        $"Bubble atualizada: {bubble} ");

        //    await Task.CompletedTask;
        //}
        //private async Task OnStructureUpdate(StructureStorageItem structure)
        //{
        //    //se quiser fazer o input de outros serviços no finalizar de structure (como por exemplo indicadores, vwap, etc)

        //    Console.WriteLine(
        //        $"Structure atualizada: {structure} ");

        //    await Task.CompletedTask;
        //}

        public void Dispose()
        {
            // unsubscribe using shared helper
            UnsubscribeAll(candleService, OnCandleUpdate);
            //UnsubscribeAll(bubbleService, OnBubbleUpdate);
            //UnsubscribeAll(volumeService, OnVolumeUpdate);
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
