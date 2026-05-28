using B3WM.Shared.Entity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExtractorRTD.Services
{
    public static class TimesAndTradesRtd
    {
        private static IRtdServer _rtdServer;

        private static RtdUpdateEvent _callback;

        private static readonly object _lock =
            new object();

        public static string url { get; set; }

        public static HubConnection hubConnection;

        public static string ConnectionState =>
            hubConnection?.State.ToString() ?? "Disconnected";

        private const int ROWS = 300;

        private static int _nextTrydId = 1;

        public static int Counter { get; set; } = 0;

        public static DateTime BaseTime { get; set; }

        // topicId -> value
        private static readonly Dictionary<int, string>
            _values =
                new Dictionary<int, string>();

        // topicId -> topic info
        private static readonly Dictionary<int, TopicInfo>
            _topics =
                new Dictionary<int, TopicInfo>();

        // trades pendentes para envio
        private static readonly List<Ticks2>
            _pendingTicks =
                new List<Ticks2>();

        // cache de contagem dos snapshots
        private static readonly ConcurrentDictionary<
            string,
            TradeCounter>
            _tradeCache =
                new ConcurrentDictionary<
                    string,
                    TradeCounter>();

        private static int _nextTopicId = 1;

        [STAThread]
        public static void Start(
            List<TntConfig> configs,
            string _url,
            BackgroundWorker worker,
            CancellationToken token)
        {
            url = _url;

            worker.ReportProgress(
                0,
                new TickLogItem
                {
                    Symbol = "SYSTEM",
                    Message = "RTD Started"
                });

            string rtdProgId =
                "rtdtrading.rtdserver";

            Type rtdType =
                Type.GetTypeFromProgID(rtdProgId);

            if (rtdType == null)
            {
                worker.ReportProgress(
                    0,
                    new TickLogItem
                    {
                        Symbol = "SYSTEM",
                        Message = "RTD não encontrado"
                    });

                return;
            }

            _rtdServer =
                (IRtdServer)
                Activator.CreateInstance(rtdType);

            _callback =
                new RtdUpdateEvent();

            int result =
                _rtdServer.ServerStart(_callback);

            worker.ReportProgress(
                0,
                new TickLogItem
                {
                    Symbol = "SYSTEM",
                    Message = $"ServerStart: {result}"
                });

            StartHubConnection();

            RegisterTopics(configs);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_callback.newData)
                    {
                        ReadRtdBatch();

                        ProcessTrades(
                            configs,
                            worker);

                        _callback.newData = false;
                    }

                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    worker.ReportProgress(
                        0,
                        new TickLogItem
                        {
                            Symbol = "ERROR",
                            Message = ex.ToString()
                        });
                }
            }

            Stop();
        }

        public static void StartHubConnection()
        {
            try
            {
                if (hubConnection != null)
                {
                    hubConnection.StopAsync()
                        .GetAwaiter()
                        .GetResult();
                }
            }
            catch
            {
            }

            hubConnection =
                new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();

            hubConnection.StartAsync()
                .GetAwaiter()
                .GetResult();
        }

        private static void RegisterTopics(
            List<TntConfig> configs)
        {
            foreach (var config in configs)
            {
                for (int row = 0; row < ROWS; row++)
                {
                    RegisterTopic(
                        config,
                        row,
                        "DAT");

                    RegisterTopic(
                        config,
                        row,
                        "PRE");

                    RegisterTopic(
                        config,
                        row,
                        "QUL");

                    RegisterTopic(
                        config,
                        row,
                        "ACP");

                    RegisterTopic(
                        config,
                        row,
                        "AVD");

                    RegisterTopic(
                        config,
                        row,
                        "AGR");
                }
            }
        }

        private static void RegisterTopic(
            TntConfig config,
            int row,
            string field)
        {
            int topicId =
                _nextTopicId++;

            Array topic =
                new object[]
                {
                    config.TNTSymbol,
                    field,
                    row.ToString()
                };

            bool newValue = true;

            _rtdServer.ConnectData(
                topicId,
                ref topic,
                ref newValue);

            _topics[topicId] =
                new TopicInfo
                {
                    TopicId = topicId,
                    Field = field,
                    Row = row,
                    TNTSymbol = config.TNTSymbol,
                    Symbol = config.Symbol
                };
        }

        private static void ReadRtdBatch()
        {
            int topicCount = 0;

            Array data =
                _rtdServer.RefreshData(
                    ref topicCount);

            if (data == null)
                return;

            lock (_lock)
            {
                for (int i = 0; i < topicCount; i++)
                {
                    int topicId =
                        Convert.ToInt32(
                            data.GetValue(0, i));

                    object raw =
                        data.GetValue(1, i);

                    string value =
                        raw?.ToString() ?? "";

                    _values[topicId] = value;
                }
            }
        }

        private static void ProcessTrades(
            List<TntConfig> configs,
            BackgroundWorker worker)
        {
            lock (_lock)
            {
                var ticks =
                    new List<Ticks2>();

                foreach (var config in configs)
                {
                    for (int row = 0; row < ROWS; row++)
                    {
                        string dat =
                            GetValue(
                                config.TNTSymbol,
                                row,
                                "DAT");

                        string pre =
                            GetValue(
                                config.TNTSymbol,
                                row,
                                "PRE");

                        string qul =
                            GetValue(
                                config.TNTSymbol,
                                row,
                                "QUL");

                        string acp =
                            GetValue(
                                config.TNTSymbol,
                                row,
                                "ACP");

                        string avd =
                            GetValue(
                                config.TNTSymbol,
                                row,
                                "AVD");

                        string agr =
                            GetValue(
                                config.TNTSymbol,
                                row,
                                "AGR");

                        if (!IsValidTrade(
                            dat,
                            pre,
                            qul))
                        {
                            continue;
                        }

                        if (!DateTime.TryParse(
                            dat,
                            out DateTime time))
                        {
                            continue;
                        }

                        if (!TryParsePrice(
                            pre,
                            out double price))
                        {
                            continue;
                        }

                        if (!int.TryParse(
                            qul,
                            out int qty))
                        {
                            continue;
                        }

                        ticks.Add(
                            new Ticks2
                            {
                                Time = time,
                                Value = price,
                                Volume = qty,
                                Buyer = ParseAgent(acp),
                                Seller = ParseAgent(avd),
                                Starter = ParseAction(agr),
                                Symbol = config.Symbol
                            });
                    }
                }

                ticks =
                    ticks
                    .OrderBy(x => x.Time)
                    .ToList();

                var grouped =
                    ticks
                    .GroupBy(BuildTradeKey)
                    .ToDictionary(
                        x => x.Key,
                        x => x.ToList());

                foreach (var kv in grouped)
                {
                    string key =
                        kv.Key;

                    List<Ticks2> sameTrades =
                        kv.Value;

                    int currentCount =
                        sameTrades.Count;

                    int alreadySent =
                        0;

                    if (_tradeCache.TryGetValue(
                        key,
                        out TradeCounter counter))
                    {
                        alreadySent =
                            counter.Count;
                    }

                    int missing =
                        currentCount - alreadySent;

                    if (missing <= 0)
                    {
                        if (_tradeCache.ContainsKey(key))
                        {
                            _tradeCache[key]
                                .LastSeen =
                                    DateTime.UtcNow;
                        }

                        continue;
                    }

                    for (int i = 0; i < missing; i++)
                    {
                        var tick =
                            sameTrades[i];

                        tick.TrydID =
                            _nextTrydId++;

                        Counter++;

                        worker.ReportProgress(
                            GetAverageTicks(
                                Counter,
                                tick.Time),
                            new TickLogItem
                            {
                                Symbol = tick.Symbol,
                                Message = tick.ToString(),
                                Tick = tick
                            });

                        _pendingTicks.Add(tick);
                    }

                    _tradeCache[key] =
                        new TradeCounter
                        {
                            Count = currentCount,
                            LastSeen = DateTime.UtcNow
                        };
                }

                CleanupTradeCache();

                if (_pendingTicks.Count > 0)
                {
                    SendTicks()
                        .GetAwaiter()
                        .GetResult();
                }
            }
        }

        private static async Task SendTicks()
        {
            try
            {
                if (_pendingTicks.Count == 0)
                    return;

                if (hubConnection == null ||
                    hubConnection.State !=
                    HubConnectionState.Connected)
                {
                    StartHubConnection();
                }

                // AGRUPA POR ATIVO
                var grouped =
                    _pendingTicks
                    .GroupBy(x => x.Symbol)
                    .ToList();

                foreach (var group in grouped)
                {
                    var arr =
                        group.ToArray();

                    string symbol =
                        group.Key;

                    await hubConnection.SendAsync(
                        "SendDataTntProfit",
                        arr,
                        symbol);
                }

                _pendingTicks.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void CleanupTradeCache()
        {
            DateTime now =
                DateTime.UtcNow;

            foreach (var kv in _tradeCache)
            {
                if ((now - kv.Value.LastSeen)
                    .TotalSeconds > 5)
                {
                    _tradeCache.TryRemove(
                        kv.Key,
                        out _);
                }
            }
        }

        private static string BuildTradeKey(
            Ticks2 tick)
        {
            return
                $"{tick.Symbol}|" +
                $"{tick.Time:HH:mm:ss.fff}|" +
                $"{tick.Value}|" +
                $"{tick.Volume}|" +
                $"{tick.Buyer}|" +
                $"{tick.Seller}|" +
                $"{tick.Starter}";
        }

        private static bool IsValidTrade(
            string dat,
            string pre,
            string qul)
        {
            if (string.IsNullOrWhiteSpace(dat))
                return false;

            if (string.IsNullOrWhiteSpace(pre))
                return false;

            if (string.IsNullOrWhiteSpace(qul))
                return false;

            if (!TryParsePrice(
                pre,
                out double price))
            {
                return false;
            }

            if (!int.TryParse(
                qul,
                out int qty))
            {
                return false;
            }

            if (price <= 0)
                return false;

            if (qty <= 0)
                return false;

            return true;
        }

        private static bool TryParsePrice(
            string value,
            out double result)
        {
            result = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (double.TryParse(
                value,
                NumberStyles.Any,
                new CultureInfo("pt-BR"),
                out result))
            {
                return true;
            }

            if (double.TryParse(
                value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out result))
            {
                return true;
            }

            value =
                value.Replace(".", ",");

            if (double.TryParse(
                value,
                NumberStyles.Any,
                new CultureInfo("pt-BR"),
                out result))
            {
                return true;
            }

            value =
                value.Replace(",", ".");

            if (double.TryParse(
                value,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out result))
            {
                return true;
            }

            return false;
        }

        private static Ticks2.Agents ParseAgent(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value =
                value
                .Replace("-", "_")
                .Replace(" ", "_");

            foreach (var name in Enum.GetNames(
                typeof(Ticks2.Agents)))
            {
                if (name.Equals(
                    value,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return
                        (Ticks2.Agents)
                        Enum.Parse(
                            typeof(Ticks2.Agents),
                            name);
                }
            }

            return 0;
        }

        private static Ticks2.ActionType ParseAction(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value =
                value
                .Trim()
                .ToLower();

            if (value.Contains("comprador"))
                return Ticks2.ActionType.Buy;

            if (value.Contains("vendedor"))
                return Ticks2.ActionType.Sale;

            if (value.Contains("rlp"))
                return Ticks2.ActionType.RLP;

            if (value.Contains("leil"))
                return Ticks2.ActionType.Auction;

            if (value.Contains("direto"))
                return Ticks2.ActionType.Cross;

            return 0;
        }

        private static string GetValue(
            string tnt,
            int row,
            string field)
        {
            foreach (var kv in _topics)
            {
                var topic =
                    kv.Value;

                if (topic.TNTSymbol == tnt &&
                    topic.Row == row &&
                    topic.Field == field)
                {
                    if (_values.TryGetValue(
                        topic.TopicId,
                        out string value))
                    {
                        return value;
                    }
                }
            }

            return "";
        }

        public static void Stop()
        {
            try
            {
                if (_rtdServer != null)
                {
                    foreach (var topicId in _topics.Keys)
                    {
                        try
                        {
                            _rtdServer
                                .DisconnectData(
                                    topicId);
                        }
                        catch
                        {
                        }
                    }

                    _rtdServer.ServerTerminate();
                }
            }
            catch
            {
            }
        }

        public static int GetAverageTicks(
            int counter,
            DateTime time)
        {
            if (BaseTime == default)
            {
                BaseTime = time;

                return counter;
            }

            double diff =
                time
                .Subtract(BaseTime)
                .TotalSeconds;

            if (diff <= 0)
                return counter;

            return
                (int)(counter / diff);
        }
    }

    public class TradeCounter
    {
        public int Count { get; set; }

        public DateTime LastSeen { get; set; }
    }

    public class TopicInfo
    {
        public int TopicId { get; set; }

        public string TNTSymbol { get; set; }

        public string Symbol { get; set; }

        public string Field { get; set; }

        public int Row { get; set; }
    }

    public class TntConfig
    {
        public string TNTSymbol { get; set; }

        public string Symbol { get; set; }
    }

    public class TickLogItem
    {
        public string Symbol { get; set; }

        public string Message { get; set; }

        public Ticks2 Tick { get; set; }
    }

    public class RtdUpdateEvent :
        IRTDUpdateEvent
    {
        public volatile bool newData =
            false;

        public int HeartbeatInterval
        {
            get;
            set;
        }

        public void Disconnect()
        {
        }

        public void UpdateNotify()
        {
            newData = true;
        }
    }
}