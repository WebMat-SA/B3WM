using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Model;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Threading.Channels;

namespace B3WM.Services.Core
{
    public class BubbleService : IProcessor<Ticks2, BubbleStorageItem>
    {
        private readonly IHubContext<DataHub, IDataHubClient> hubContext;

        private readonly Channel<Ticks2[]> _channel =
            Channel.CreateUnbounded<Ticks2[]>();

        public event Func<BubbleStorageItem, Task>? OnUpdate;

        private int _bubbleThreshold = 350;

        // running state
        private int _runningSum = 0;
        private Ticks2.Agents? _runningAgent = null;
        private Ticks2.ActionType? _runningStarter = null;
        private double _lastPrice = 0;
        private DateTime _lastTime = default;
        private string _lastSymbol = string.Empty;

        public BubbleService(IHubContext<DataHub, IDataHubClient> hubContext = null)
        {
            this.hubContext = hubContext;

            _ = Task.Run(ProcessLoop);
        }

        public void Enqueue(Ticks2[] ticks)
        {
            _channel.Writer.TryWrite(ticks);
        }

        public object GetSnapshot() => new
        {
            BubbleThreshold = _bubbleThreshold,
            RunningSum = _runningSum,
            RunningAgent = _runningAgent,
            RunningStarter = _runningStarter,
            LastPrice = _lastPrice,
            LastTime = _lastTime,
            LastSymbol = _lastSymbol,
        };

        private async Task ProcessLoop()
        {
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
                    Console.WriteLine($"BubbleService.ProcessLoop error: {ex.Message}");
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
                var bubble = FinalizeRunning();
                if (bubble != null && OnUpdate != null)
                {
                    try
                    {
                        await OnUpdate.Invoke(bubble);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OnUpdate error: {ex.Message}");
                    }
                }
                return;
            }

            // changed aggressor or side -> finalize previous
            if (_runningAgent != null && (_runningAgent != aggressor || _runningStarter != t.Starter))
            {
                var bubble = FinalizeRunning();
                if (bubble != null && OnUpdate != null)
                {
                    try
                    {

                        //ainda pensar sobre signalR e envio de dados para clientes
                        if (hubContext != null)
                        {
                            await hubContext.Clients.Group(bubble.Symbol).ReceiveOnBubble(bubble);
                        }

                        await OnUpdate.Invoke(bubble);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OnUpdate error: {ex.Message}");
                    }
                }
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

        private BubbleStorageItem? FinalizeRunning()
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

                    //Console.WriteLine("Bubble detected: " + bubble.ToString());

                    // reset
                    _runningSum = 0;
                    _runningAgent = null;
                    _runningStarter = null;

                    return bubble;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FinalizeRunning error: " + ex.Message);
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
