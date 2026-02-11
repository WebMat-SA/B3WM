using B3WM.Shared.Entity;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace B3WM.Client.Services
{
    public class DataHelper
    {

        byte[]? data;
        public DataHelper(byte[] data)
        {
            this.data = data;
        }

        public ICollection<Ticks2> TimesAndTrades(string prefix = "NEGS!")
        {
            ICollection<Ticks2> TicksQueue = new Collection<Ticks2>();

            string textData = Encoding.UTF8.GetString(data);

            string[] manyPapersInfo = (textData).Split(new[] { "#" }, StringSplitOptions.None);

            foreach (string onePaperInfo in manyPapersInfo)
            {
                if (string.IsNullOrEmpty(onePaperInfo))
                    continue;

                string[] parameters = onePaperInfo.Split(new[] { "|" }, StringSplitOptions.None);
                if (parameters.Length >= 7)
                {
                    // pre-split columns once to avoid repeated allocations
                    var col0 = parameters[0].Split(new[] { prefix }, StringSplitOptions.None);
                    string paper = col0.Length > 1 ? col0[1] : col0[0];

                    string[] trydIds = parameters[1].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] times = parameters[2].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] values = parameters[3].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] volumes = parameters[4].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] buyers = parameters[5].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] sellers = parameters[6].Split(new[] { "@" }, StringSplitOptions.None);
                    string[] starters = parameters.Length > 7 ? parameters[7].Split(new[] { "@" }, StringSplitOptions.None) : null;

                    int lines = trydIds.Length;

                    for (int i = 0; i < lines; i++)
                    {
                        try
                        {
                            // parse fields using TryParse to avoid exceptions
                            if (!int.TryParse(trydIds[i], out int trydId))
                                continue;

                            if (!DateTime.TryParseExact(times[i], "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
                                continue;

                            if (!double.TryParse(values[i], NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                                continue;

                            if (!int.TryParse(volumes[i], out int volume))
                                continue;

                            if (!int.TryParse(buyers[i], out int buyerInt))
                                continue;

                            if (!int.TryParse(sellers[i], out int sellerInt))
                                continue;

                            var tick = new Ticks2
                            {
                                TrydID = trydId,
                                Time = time,
                                Value = value,
                                Volume = volume,
                                Buyer = (Ticks2.Agents)buyerInt,
                                Seller = (Ticks2.Agents)sellerInt
                            };

                            if (starters != null && starters.Length > 0)
                            {
                                string starterValue = (i < starters.Length) ? starters[i] : starters[0];
                                tick.Starter = starterValue == "Comprador" ? Ticks2.ActionType.Buy : (starterValue == "Vendedor" ? Ticks2.ActionType.Sale : (starterValue == "Cross" ? Ticks2.ActionType.Cross : Ticks2.ActionType.Auction));
                            }
                            else
                            {
                                tick.Starter = Ticks2.ActionType.Auction;
                            }

                            TicksQueue.Add(tick);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine("TimesAndSales - " + err.Message);
                            Console.WriteLine(onePaperInfo);
                        }
                    }
                }
            }

            return TicksQueue;
        }

        public List<BookItem> Book()
        {
            List<BookItem> PseudoBook = new List<BookItem>();

            string[] manyPapersInfo = (Encoding.UTF8.GetString(data)).Split('#');

            for (int paperinfoCount = manyPapersInfo.Length - 1; paperinfoCount >= 0; paperinfoCount--)
            {
                string onePaperInfo = manyPapersInfo[paperinfoCount];

                if (string.IsNullOrEmpty(onePaperInfo))
                    continue;

                string[] parameters = onePaperInfo.Split('|');
                if (parameters.Length <= 1)
                    continue;

                try
                {
                    string paper = parameters[0].Replace("LVL2!", "").ToUpper();

                    // iterate book parameters and split each only once
                    BookItem bi = null;
                    for (int bookParam = 1; bookParam < parameters.Length; bookParam++)
                    {
                        string[] bookitem = parameters[bookParam].Split(';');

                        if (bookitem.Length != 4)
                            continue;

                        if (!int.TryParse(bookitem[2], out int coluna))
                            continue;

                        string content = bookitem[3];

                        switch (coluna)
                        {
                            case 0:
                                if (!int.TryParse(content, out int agent0))
                                    continue;

                                bi = new BookItem();
                                bi.Agent = (Ticks2.Agents)agent0;
                                break;
                            case 1:
                                if (bi == null)
                                    bi = new BookItem();

                                if (!int.TryParse(content, out int volume1))
                                    continue;

                                bi.Volume = volume1;
                                break;
                            case 2:
                                if (bi == null)
                                    bi = new BookItem();

                                if (content == "Aber.")
                                    bi.Value = -1.0d;
                                else if (double.TryParse(content, NumberStyles.Any, CultureInfo.InvariantCulture, out double val2))
                                    bi.Value = val2;
                                else
                                    continue;

                                bi.Type = Ticks2.ActionType.Buy;
                                PseudoBook.Add(bi);
                                bi = null;
                                break;
                            case 3:
                                if (bi == null)
                                    bi = new BookItem();

                                if (content == "Aber.")
                                    bi.Value = -1.0d;
                                else if (double.TryParse(content, NumberStyles.Any, CultureInfo.InvariantCulture, out double val3))
                                    bi.Value = val3;
                                else
                                    continue;

                                break;
                            case 4:
                                if (bi == null)
                                    bi = new BookItem();

                                if (!int.TryParse(content, out int volume4))
                                    continue;

                                bi.Volume = volume4;
                                break;
                            case 5:
                                if (bi == null)
                                    bi = new BookItem();

                                if (!int.TryParse(content, out int agent5))
                                    continue;

                                bi.Agent = (Ticks2.Agents)agent5;
                                bi.Type = Ticks2.ActionType.Sale;

                                PseudoBook.Add(bi);
                                bi = null;
                                break;
                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine("BookService - " + err.Message);
                    Console.WriteLine(onePaperInfo);
                }
            }

            return PseudoBook;
        }


    }

    public class QueueHelper
    {
        private readonly Action<(double price, Ticks2.Agents? agent, decimal amount, DateTime time)>? OnNewBubble;
        private readonly int BubbleThreshold;

        private ConcurrentQueue<byte[]> queue { get; set; } = new ConcurrentQueue<byte[]> ();

        // 0 = not processing, 1 = processing
        private int _isProcessing = 0;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public QueueHelper(Action<(double price, Ticks2.Agents? agent, decimal amount, DateTime time)> _OnNewBubble, int bubbleThreshold = 125)
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
