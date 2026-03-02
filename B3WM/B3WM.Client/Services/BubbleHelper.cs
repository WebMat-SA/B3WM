using B3WM.Shared.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace B3WM.Client.Services
{
    public class BubbleHelper : IDisposable
    {
        public event EventHandler<Bubble>? OnNewBubble;
        public event EventHandler<int>? OnQueueCount;

        private readonly ConcurrentQueue<Ticks2[]> _queue = new();
        private readonly CancellationTokenSource _cts = new();

        private int _bubbleThreshold { get; set; }

        // 🔥 Estado contínuo
        private int _runningSum = 0;
        private Ticks2.Agents? _runningAgent = null;
        private Ticks2.ActionType? _runningStarter = null;
        private double _lastPrice = 0;
        private DateTime _lastTime;

        private PeriodicTimer? _timer;

        private bool NotifyQueueCount { get; set; }

        private int _queueCount { get; set; }

        public void Init(int throtlingms = 200, int bubbleThreshold = 125, bool _notifyqueue = true)
        {
            _bubbleThreshold = bubbleThreshold;
            NotifyQueueCount = _notifyqueue;

            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));
            _ = RunLoop();
        }
        private async Task RunLoop()
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                if (NotifyQueueCount) OnQueueCount?.Invoke(this, _queueCount);
            }
        }

        public void Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return;

            _queue.Enqueue(ticks);

            _ = ProcessQueueAsync();

            //return Task.CompletedTask;
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
                    var bubble = new Bubble
                    {
                        Price = _lastPrice,
                        Agent = _runningAgent,
                        Amount = _runningSum, // 🔥 agora envia o TOTAL acumulado
                        Time = _lastTime,
                        ActionType = _runningStarter
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

        public void ToggleNotifyQueue()
        {
            _queueCount = 0;
            OnQueueCount?.Invoke(this, _queueCount);
            NotifyQueueCount = !NotifyQueueCount;
        }
    }
}
