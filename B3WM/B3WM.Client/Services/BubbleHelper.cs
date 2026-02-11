using B3WM.Shared.Entity;
using System.Collections.Concurrent;

namespace B3WM.Client.Services
{
    public class BubbleHelper
    {
        private readonly Action<(double price, Ticks2.Agents? agent, decimal amount, DateTime time)>? OnNewBubble;
        private readonly int BubbleThreshold;

        private ConcurrentQueue<byte[]> queue { get; set; } = new ConcurrentQueue<byte[]>();

        // 0 = not processing, 1 = processing
        private int _isProcessing = 0;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public BubbleHelper(Action<(double price, Ticks2.Agents? agent, decimal amount, DateTime time)> _OnNewBubble, int bubbleThreshold = 125)
        {
            OnNewBubble = _OnNewBubble;
            BubbleThreshold = bubbleThreshold;
        }

        public Task Enqueue(byte[] data)
        {
            if (data == null) return Task.CompletedTask;

            queue.Enqueue(data);

            // If not already processing, start a background task to process the queue
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
            {
                // fire-and-forget the processor
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
                    // Dequeue and process all available items
                    while (queue.TryDequeue(out var item))
                    {
                        try
                        {
                            if (item == null) continue;

                            var helper = new DataHelper(item);
                            var ticks = helper.TimesAndTrades("NEG!");

                            // Accumulate consecutive aggressions from the same side/agent
                            int runningSum = 0;
                            Ticks2.Agents? runningAgent = null;
                            Ticks2.ActionType? runningStarter = null;

                            foreach (var t in ticks)
                            {
                                try
                                {
                                    Console.WriteLine(t.ToString());

                                    // Determine who was the aggressor for this trade: starter indicates Buy/Sale
                                    Ticks2.ActionType starter = t.Starter;
                                    Ticks2.Agents? aggressor = null;

                                    if (starter == Ticks2.ActionType.Buy)
                                        aggressor = t.Buyer;
                                    else if (starter == Ticks2.ActionType.Sale)
                                        aggressor = t.Seller;

                                    // If we don't have a clear aggressor, treat this tick as not part of a running aggression
                                    if (aggressor == null)
                                    {
                                        // fallback: if a single tick itself exceeds threshold, still report it
                                        if (t.Volume >= BubbleThreshold)
                                        {
                                            try
                                            {
                                                OnNewBubble?.Invoke((t.Value, t.Buyer, Convert.ToDecimal(t.Volume), t.Time));
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine("OnNewBubble handler threw: " + ex.Message);
                                            }
                                        }

                                        // reset running accumulation
                                        runningSum = 0;
                                        runningAgent = null;
                                        runningStarter = null;

                                        continue;
                                    }

                                    // If this tick continues the same running aggression, accumulate; otherwise reset
                                    if (runningAgent == null || runningAgent != aggressor || runningStarter != starter)
                                    {
                                        runningAgent = aggressor;
                                        runningStarter = starter;
                                        runningSum = t.Volume;
                                    }
                                    else
                                    {
                                        runningSum += t.Volume;
                                    }

                                    // If accumulated volume reaches threshold, report a bubble
                                    if (runningSum >= BubbleThreshold)
                                    {
                                        try
                                        {
                                            OnNewBubble?.Invoke((t.Value, runningAgent, Convert.ToDecimal(runningSum), t.Time));
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("OnNewBubble handler threw: " + ex.Message);
                                        }

                                        // reset after reporting to avoid duplicate events for the same accumulation
                                        runningSum = 0;
                                        runningAgent = null;
                                        runningStarter = null;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine("QueueHelper processing item failed: " + ex.Message);
                                }

                                // yield to avoid starving the thread (kept as before)
                                await Task.Yield();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("QueueHelper processing item failed: " + ex.Message);
                        }

                        // yield to avoid starving the thread
                        await Task.Yield();
                    }

                    // no more items - set processing flag to 0 and exit unless new items arrived
                    Interlocked.Exchange(ref _isProcessing, 0);

                    // if new items were added after we emptied the queue, attempt to take ownership and continue
                    if (!queue.IsEmpty && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                    {
                        continue; // loop again to process remaining items
                    }

                    break; // nothing more to do
                }
            }
            catch (OperationCanceledException)
            {
                // expected on dispose
            }
            catch (Exception ex)
            {
                Console.WriteLine("QueueHelper background processor failed: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _isProcessing, 0);
            }
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
            }
            catch { }
        }
    }
}
