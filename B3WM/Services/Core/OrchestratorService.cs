using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services.Core
{
    public class OrchestratorService : IDisposable, ISymbolable
    {
        private readonly IEnumerable<CandleService> candleService;
        private readonly IEnumerable<BubbleService> bubbleService;
        private readonly IEnumerable<VolumeService> volumeService;
        private readonly IEnumerable<StructureService> structureService;
        private readonly IEnumerable<VwapService> vwapService;
        private readonly ILogger<OrchestratorService> _logger;
        public string Symbol { get; }

        /// <summary>Disparado ao final do fechamento de um candle, com a barra finalizada (volume/forecast) e a estrutura calculada (pode ser nula em fallback: o evento sempre dispara).</summary>
        public event Func<BarStorageItem, StructureStorageItem?, Task>? OnCandleClosed;

        /// <summary>Repassa as bubbles do símbolo (o broadcast SignalR segue no BubbleService; aqui é só forward in-process p/ consumidores como o StrategyRunner).</summary>
        public event Func<BubbleStorageItem, Task>? OnBubble;

        public OrchestratorService(string Symbol, IHubContext<DataHub, IDataHubClient> hubContext,IEnumerable<CandleService> candleService, IEnumerable<BubbleService> bubbleService, IEnumerable<VolumeService> volumeService, IEnumerable<StructureService> structureService, IEnumerable<VwapService> vwapService, ILogger<OrchestratorService> logger)
        {
            this.Symbol = Symbol;

            this.candleService = candleService.Where(q=>q.Symbol == Symbol);
            this.bubbleService = bubbleService.Where(q=>q.Symbol == Symbol);
            this.volumeService = volumeService.Where(q=>q.Symbol == Symbol);
            this.structureService = structureService.Where(q=>q.Symbol == Symbol);
            this.vwapService = vwapService.Where(q=>q.Symbol == Symbol);

            _logger = logger;
            SubscribeAll(this.candleService, OnCandleUpdate);
            SubscribeAll(this.bubbleService, OnBubbleForward);
        }

        public Task Enqueue(Ticks2[] ticks)
        {

            // enqueue to all processors using shared helper
            // VwapService NÃO consome ticks direto: é disparado no fechamento do 1MIN.
            EnqueueAll(candleService, ticks);
            EnqueueAll(bubbleService, ticks);
            EnqueueAll(volumeService, ticks);

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

                #region Vwap in Candles (fonte única: VwapService via 1MIN)
                // Se a barra fechada é o 1MIN, alimenta a VWAP antes de anexar.
                if (bar.TimeFrame == 1)
                {
                    try
                    {
                        var vwapSvc = vwapService.FirstOrDefault();
                        if (vwapSvc != null)
                            await vwapSvc.AppendClosedBarAsync(bar);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "OrchestratorService.Vwap append error");
                    }
                }

                var vwapSnapshot = vwapService.FirstOrDefault()?.GetSnapshot();

                if (vwapSnapshot != null && vwapSnapshot.Vwap > 0)
                    bar.VwapPrice = vwapSnapshot.Vwap;

                #endregion

                #region Structure by Candles
                var structureService = this.structureService.FirstOrDefault(q=>q.TimeFrame == bar.TimeFrame);

                StructureStorageItem? structure = null;
                if (structureService != null)
                {
                    structure = await structureService.Calculate(bar);
                }

                #endregion

                // O fechamento do candle é o fato: dispara sempre (mesmo com
                // structure nula em fallback) para não perder execução de
                // consumidores (ex.: StrategyRunner: pending/virada/persist).
                if (OnCandleClosed != null)
                    await OnCandleClosed.Invoke(bar, structure);

                _logger.LogDebug("Bar atualizado: {Bar}", bar);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OrchestratorService.OnCandleUpdate error");
            }
        }

        private async Task OnBubbleForward(BubbleStorageItem bubble)
        {
            try
            {
                if (OnBubble != null)
                    await OnBubble.Invoke(bubble);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OrchestratorService.OnBubbleForward error");
            }
        }

        public void Dispose()
        {
            UnsubscribeAll(candleService, OnCandleUpdate);
            UnsubscribeAll(bubbleService, OnBubbleForward);
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
