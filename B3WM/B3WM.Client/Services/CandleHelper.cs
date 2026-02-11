using B3WM.Shared.Entity;
using System.Collections.Concurrent;

namespace B3WM.Client.Services
{
    public class CandleHelper
    {
        private readonly Action<IEnumerable<Bars>>? OnNewBars;
        private readonly int TimeFrameMinutes;

        private ConcurrentQueue<byte[]> queue { get; set; } = new ConcurrentQueue<byte[]>();

        // 0 = not processing, 1 = processing
        private int _isProcessing = 0;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public CandleHelper(Action<IEnumerable<Bars>> _OnNewBars, int _TimeFrameMinutes = 5)
        {
            OnNewBars = _OnNewBars;
            TimeFrameMinutes = _TimeFrameMinutes;
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

                            foreach (var t in ticks)
                            {
                                try
                                {
                                    Console.WriteLine(t.ToString());

                                    //fazer logica aqui de agrupamento dos ticks em candles de 5 minutos, e chamar OnNewBars com os candles formados


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
