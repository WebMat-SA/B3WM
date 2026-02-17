using B3WM.Shared.Entity;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace B3WM.Client.Services
{
    public class BubbleHelper : IDisposable
    {
        private const int YieldEveryTicks = 256;
        private readonly ConcurrentQueue<byte[]> queue = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly Action<Bubble>? OnNewBubble;
        private readonly int BubbleThreshold;


        private int _isProcessing = 0;
        private int _pendingChunks = 0;
        private int _batchSequence = 0;

        // 🔥 Estado contínuo
        private int _runningSum = 0;
        private Ticks2.Agents? _runningAgent = null;
        private Ticks2.ActionType? _runningStarter = null;
        private double _lastPrice = 0;
        private DateTime _lastTime;
        private int _emittedBubblesInBatch;
        private long _emitCallbackMsInBatch;

        public BubbleHelper(Action<Bubble> onNewBubble, int bubbleThreshold = 125)
        {
            OnNewBubble = onNewBubble;
            BubbleThreshold = bubbleThreshold;
        }

        public Task Enqueue(byte[] data)
        {
            if (data == null) return Task.CompletedTask;

            queue.Enqueue(data);
            Interlocked.Increment(ref _pendingChunks);

            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
            {
                _ = Task.Run(ProcessQueueAsync, _cts.Token);
            }

            return Task.CompletedTask;
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                var token = _cts.Token;

                while (!token.IsCancellationRequested)
                {
                    var sw = Stopwatch.StartNew();
                    var swParse = Stopwatch.StartNew();
                    int chunks = 0, tickCount = 0;
                    int processedTicksSinceYield = 0;
                    _emittedBubblesInBatch = 0;
                    _emitCallbackMsInBatch = 0;
                    long parseMs = 0;
                    long tickProcessingMs = 0;

                    while (queue.TryDequeue(out var item))
                    {
                        chunks++;
                        Interlocked.Decrement(ref _pendingChunks);

                        swParse.Restart();
                        var helper = new DataHelper(item);
                        var ticks = helper.TimesAndTrades();
                        swParse.Stop();
                        parseMs += swParse.ElapsedMilliseconds;
                        tickCount += ticks.Count;

                        var swTicks = Stopwatch.StartNew();
                        foreach (var t in ticks)
                        {
                            ProcessTick(t);
                            processedTicksSinceYield++;
                            if ((processedTicksSinceYield & (YieldEveryTicks - 1)) == 0)
                                await Task.Yield();
                        }
                        swTicks.Stop();
                        tickProcessingMs += swTicks.ElapsedMilliseconds;
                    }

                    sw.Stop();
                    if (chunks > 0)
                    {
                        var seq = Interlocked.Increment(ref _batchSequence);
                        var pending = Volatile.Read(ref _pendingChunks);
                        HelperPerformanceConfig.LogSampled(
                            nameof(BubbleHelper),
                            "ProcessQueueAsync",
                            sw.ElapsedMilliseconds,
                            seq,
                            $"chunks={chunks} ticks={tickCount} parseMs={parseMs} tickMs={tickProcessingMs} emitCount={_emittedBubblesInBatch} emitMs={_emitCallbackMsInBatch} pending={pending}");
                    }

                    Interlocked.Exchange(ref _isProcessing, 0);

                    if (!queue.IsEmpty &&
                        Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                        continue;

                    break;
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
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
            if (_runningAgent != null && _runningSum >= BubbleThreshold)
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
                    OnNewBubble?.Invoke(bubble);
                    sw.Stop();
                    _emittedBubblesInBatch++;
                    _emitCallbackMsInBatch += sw.ElapsedMilliseconds;
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
            try { _cts.Cancel(); } catch { }
        }
    }
}
