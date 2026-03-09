using B3WM.Client.Model;
using B3WM.Shared.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace B3WM.Client.Services
{
    public class BubbleHelper : IDisposable
    {
        public event EventHandler<BubbleStorageItem>? OnNewBubble;
        public event EventHandler<int>? OnQueueCount;
        public event EventHandler<string>? OnQueueTime;

        private readonly ConcurrentQueue<Ticks2[]> _queue = new();
        private readonly CancellationTokenSource _cts = new();

        private int _bubbleThreshold { get; set; }

        // 🔥 Estado contínuo
        private int _runningSum = 0;
        private Ticks2.Agents? _runningAgent = null;
        private Ticks2.ActionType? _runningStarter = null;
        private double _lastPrice = 0;
        private DateTime _lastTime;
        private string _lastSymbol;

        private PeriodicTimer? _timer;

        private int _queueCount { get; set; }
        private string _queueTime { get; set; }

        public void Init(int throtlingms = 200, int bubbleThreshold = 125)
        {
            _bubbleThreshold = bubbleThreshold;

            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));
            _ = RunLoop();
        }
        private async Task RunLoop()
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                OnQueueCount?.Invoke(this, _queueCount);
                OnQueueTime?.Invoke(this, _queueTime);
                
            }
        }

        public void Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return;

            _queue.Enqueue(ticks);

            _ = ProcessQueueAsync();
        }

        private Task ProcessQueueAsync()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                long tickProcessingMs = 0;

                while (_queue.TryDequeue(out var ticks))
                {

                    var swTicks = Stopwatch.StartNew();
                    var sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();
                    _queueCount = sortedTicks.Count;
                    _queueTime = sortedTicks.Last().Time.ToString("HH:mm:ss");

                    foreach (var t in sortedTicks)
                    {
                        ProcessTick(t);
                    }
                    swTicks.Stop();
                    tickProcessingMs += swTicks.ElapsedMilliseconds;
                }

                sw.Stop();

                HelperPerformanceConfig.Log(
                        nameof(BubbleHelper),
                        "ProcessQueueAsync",
                        sw.ElapsedMilliseconds,
                        $"tickMs={tickProcessingMs}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"BubbleHelper.ProcessQueueAsync error: {ex.Message}");
            }
            finally
            {
                sw.Stop();
            }

            return Task.CompletedTask;
        }

        private void ProcessTick(Ticks2 t)
        {
            Ticks2.Agents? aggressor = null;
            _lastSymbol = t.Symbol;

            if (t.Starter == Ticks2.ActionType.Buy)
                aggressor = t.Buyer;
            else if (t.Starter == Ticks2.ActionType.Sale)
                aggressor = t.Seller;

            // se não há agressor claro → finaliza sequência
            if (aggressor == null)
            {
                FinalizeRunning();
                return;
            }

            // mudou agressor ou lado → finaliza sequência anterior
            if (_runningAgent != null &&
                (_runningAgent != aggressor || _runningStarter != t.Starter))
            {
                FinalizeRunning();
            }

            // inicia nova sequência se necessário
            if (_runningAgent == null)
            {
                _runningAgent = aggressor;
                _runningStarter = t.Starter;
                _runningSum = 0;

            }

            // acumula
            _runningSum += t.Volume;
            _lastPrice = t.Value;
            _lastTime = t.Time;
        }

        private void FinalizeRunning()
        {
            if (_runningAgent != null && _runningSum >= _bubbleThreshold)
            {
                try
                {
                    var bubble = new BubbleStorageItem
                    {
                        Price = _lastPrice,
                        Agent = (int)_runningAgent,
                        Amount = _runningSum, // 🔥 agora envia o TOTAL acumulado
                        Date = _lastTime,
                        ActionType = _runningStarter ?? default,
                        Symbol = _lastSymbol
                    };

                    var sw = Stopwatch.StartNew();
                    OnNewBubble?.Invoke(this, bubble);
                    sw.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("OnNewBubble error: " + ex.Message);
                }
            }

            // reset
            _runningSum = 0;
            _runningAgent = null;
            _runningStarter = null;
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
