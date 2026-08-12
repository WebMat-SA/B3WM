using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Threading.Channels;

namespace B3WM.Services.Core
{
    public class BubbleService : DataKeeperService<List<BubbleStorageItem>> ,IProcessor<Ticks2, BubbleStorageItem>, ISymbolable
    {
        public string Symbol { get; }

        private readonly IHubContext<DataHub, IDataHubClient> hubContext;
        private readonly ILogger<BubbleService> _logger;

        private readonly Channel<Ticks2[]> _channel =
            Channel.CreateUnbounded<Ticks2[]>();

        public event Func<BubbleStorageItem, Task>? OnUpdate;

        public int _bubbleThreshold { get; private set; }

        // running state
        private int _runningSum = 0;
        private Ticks2.Agents? _runningAgent = null;
        private Ticks2.ActionType? _runningStarter = null;
        private double _lastPrice = 0;
        private DateTime _lastTime = default;
        private string _lastSymbol = string.Empty;

        private BubbleStorageItem _lastItem { get; set; }  = new BubbleStorageItem();

        public override string Path => $"{Symbol}_{nameof(BubbleService)}_{DateTime.Now:yyyy-MM-dd}.json";

        public BubbleService(string symbol, int bubbleThreshold, IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider, ILogger<BubbleService> logger)
            : base(serviceProvider)
        {
            Symbol = symbol;
            _bubbleThreshold = bubbleThreshold;
            this.hubContext = hubContext;
            _logger = logger;

            _ = Task.Run(ProcessLoop);
        }

        public void Enqueue(Ticks2[] ticks)
        {
            _channel.Writer.TryWrite(ticks);
        }

        public BubbleStorageItem GetSnapshot() => _lastItem;

        private async Task ProcessLoop()
        {
            // se houver arquivo no sistema com a especificação desse serviço, já carrega na memória para evitar perda de dados.
            await LoadAsync();

            await foreach (var ticks in _channel.Reader.ReadAllAsync())
            {
                try
                {
                    IList<Ticks2> sortedTicks = new List<Ticks2>();

                    sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();

                    var swTicks = Stopwatch.StartNew();
                    foreach (var t in sortedTicks)
                    {
                        await ProcessTick(t);
                    }
                    swTicks.Stop();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BubbleService.ProcessLoop error");
                }
            }
        }

        private async Task ProcessTick(Ticks2 t)
        {
            Ticks2.Agents? aggressor = null;
            _lastSymbol = t.Symbol;

            // ignore auctions
            if (t.Starter == Ticks2.ActionType.Auction)
                return;

            if (t.Starter == Ticks2.ActionType.Buy)
                aggressor = t.Buyer;
            else if (t.Starter == Ticks2.ActionType.Sale)
                aggressor = t.Seller;

            // if no aggressor, finalize running sequence
            if (aggressor == null)
            {
                await FinalizeRunning();
                return;
            }

            // changed aggressor or side -> finalize previous
            if (_runningAgent != null && (_runningAgent != aggressor || _runningStarter != t.Starter))
            {
                await FinalizeRunning();
            }

            // start new sequence if needed
            if (_runningAgent == null)
            {
                _runningAgent = aggressor;
                _runningStarter = t.Starter;
                _runningSum = 0;
            }

            // accumulate
            _runningSum += t.Volume;
            _lastPrice = t.Value;
            _lastTime = t.Time;
        }

        private async Task<BubbleStorageItem?> FinalizeRunning()
        {
            if (_runningAgent != null && _runningSum >= _bubbleThreshold)
            {
                try
                {
                    var bubble = new BubbleStorageItem
                    {
                        Price = _lastPrice,
                        Agent = (int)_runningAgent,
                        Amount = _runningSum,
                        Date = _lastTime,
                        ActionType = _runningStarter ?? default,
                        Symbol = _lastSymbol
                    };

                    _runningSum = 0;
                    _runningAgent = null;
                    _runningStarter = null;
                    if (hubContext != null)
                    {
                        await hubContext.Clients.Group(bubble.Symbol).ReceiveOnBubble(bubble);
                    }

                    if (OnUpdate != null) await OnUpdate.Invoke(bubble);

                    //adiciona na lista geral
                    DataKeep.Add(bubble);

                    //marca para salvar no arquivo
                    await SetDataAsync(DataKeep);

                    return bubble;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FinalizeRunning error");
                }
            }

            // reset even when not emitted
            _runningSum = 0;
            _runningAgent = null;
            _runningStarter = null;

            return null;
        }

        public void SetBubbleThreshold(int threshold)
        {
            _bubbleThreshold = threshold;
        }
    }
}
