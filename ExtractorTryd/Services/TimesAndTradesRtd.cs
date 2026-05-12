using B3WM.Shared.Entity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

namespace ExtractorTryd.Services
{
    public static class TimesAndTradesRtd
    {
        private static IRtdServer _rtdServer;

        private static RtdUpdateEvent _callback;

        private static readonly object _lock =
            new object();

        public static string url { get; set; }

        // HUB
        private static HubConnection hubConnection;

        // ATIVO
        private static string SYMBOL = "WINFUT";
        private static string TNTSymbol = "T&T0";

        // QUANTIDADE DE LINHAS
        private const int ROWS = 300;

        // ID SEQUENCIAL
        private static int _nextTrydId = 1;

        // TO DEBUG
        public static int Counter { get; set; } = 0;

        public static DateTime BaseTime { get; set; }

        // VALORES RTD
        private static readonly Dictionary<int, string> _values =
            new Dictionary<int, string>();

        // TOPICS
        private static readonly Dictionary<int, string> _topicNames =
            new Dictionary<int, string>();

        // CONTROLE DUPLICIDADE
        private static readonly Dictionary<string, int> _sentCount =
            new Dictionary<string, int>();

        // BUFFER PENDENTE
        private static readonly List<Ticks2> _pendingTicks =
            new List<Ticks2>();

        private static int _nextTopicId = 1;

        [STAThread]
        public static void Start(string _symbol, string _tntSymbol,  string _url, BackgroundWorker worker)
        {
            url = _url;
            SYMBOL = _symbol;
            TNTSymbol = _tntSymbol;

            string rtdProgId =
                "rtdtrading.rtdserver";

            Type rtdType =
                Type.GetTypeFromProgID(rtdProgId);

            if (rtdType == null)
            {
                Console.WriteLine(
                    "RTD não encontrado");

                return;
            }

            _rtdServer =
                (IRtdServer)
                Activator.CreateInstance(rtdType);

            _callback =
                new RtdUpdateEvent();

            int result =
                _rtdServer.ServerStart(_callback);

            Console.WriteLine(
                $"ServerStart: {result}");

            try
            {
                StartHubConnection();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            RegisterTopics();

            Console.WriteLine(
                "RTD conectado.");

            Console.WriteLine();

            while (true)
            {
                try
                {
                    if (_callback.newData)
                    {
                        ReadRtdBatch();

                        ProcessTrades(worker);

                        _callback.newData = false;
                    }

                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public static void StartHubConnection()
        {
            if (hubConnection != null)
                hubConnection.DisposeAsync();

            hubConnection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();

            hubConnection.StartAsync().Wait();
        }

        private static void RegisterTopics()
        {
            for (int row = 0; row < ROWS; row++)
            {
                RegisterTopic(row, "DAT");
                RegisterTopic(row, "PRE");
                RegisterTopic(row, "QUL");
                RegisterTopic(row, "ACP");
                RegisterTopic(row, "AVD");
                RegisterTopic(row, "AGR");
            }
        }

        private static void RegisterTopic(
            int row,
            string field)
        {
            int topicId =
                _nextTopicId++;

            Array topic =
                new object[]
                {
                    TNTSymbol,
                    field,
                    row.ToString()
                };

            bool newValue = true;

            _rtdServer.ConnectData(
                topicId,
                ref topic,
                ref newValue);

            _topicNames[topicId] =
                field + "_" + row;
        }

        private static void ReadRtdBatch()
        {
            int topicCount = 0;

            Array data =
                _rtdServer.RefreshData(ref topicCount);

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
                        raw == null
                            ? ""
                            : raw.ToString();

                    _values[topicId] = value;
                }
            }
        }

        private static void ProcessTrades(BackgroundWorker worker)
        {
            lock (_lock)
            {
                var ticks =
                    new List<Ticks2>();

                for (int row = 0; row < ROWS; row++)
                {
                    string dat =
                        GetValue(row, "DAT");

                    string pre =
                        GetValue(row, "PRE");

                    string qul =
                        GetValue(row, "QUL");

                    string acp =
                        GetValue(row, "ACP");

                    string avd =
                        GetValue(row, "AVD");

                    string agr =
                        GetValue(row, "AGR");

                    if (!IsValidTrade(
                        dat,
                        pre,
                        qul))
                    {
                        continue;
                    }

                    DateTime time;

                    if (!DateTime.TryParse(
                        dat,
                        out time))
                    {
                        continue;
                    }

                    double price;

                    if (!double.TryParse(
                        pre,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out price))
                    {
                        continue;
                    }

                    int qty;

                    if (!int.TryParse(
                        qul,
                        out qty))
                    {
                        continue;
                    }

                    var tick =
                        new Ticks2
                        {
                            Time = time,
                            Value = price,
                            Volume = qty,
                            Buyer = ParseAgent(acp),
                            Seller = ParseAgent(avd),
                            Starter = ParseAction(agr),
                            Symbol = SYMBOL
                        };

                    ticks.Add(tick);
                }

                ticks =
                    ticks
                    .OrderBy(x => x.Time)
                    .ToList();

                var currentBatchCount =
                    new Dictionary<string, int>();

                foreach (var tick in ticks)
                {
                    string key =
                        tick.Time.ToString("HH:mm:ss.fff") + "|" +
                        tick.Value + "|" +
                        tick.Volume + "|" +
                        tick.Buyer + "|" +
                        tick.Seller + "|" +
                        tick.Starter;

                    if (!currentBatchCount.ContainsKey(key))
                        currentBatchCount[key] = 0;

                    currentBatchCount[key]++;
                }

                foreach (var kv in currentBatchCount)
                {
                    string key =
                        kv.Key;

                    int currentCount =
                        kv.Value;

                    int alreadySent = 0;

                    _sentCount.TryGetValue(
                        key,
                        out alreadySent);

                    int missing =
                        currentCount - alreadySent;

                    if (missing <= 0)
                        continue;

                    var parts =
                        key.Split('|');

                    DateTime time =
                        DateTime.ParseExact(
                            parts[0],
                            "HH:mm:ss.fff",
                            CultureInfo.InvariantCulture);

                    double value =
                        Convert.ToDouble(
                            parts[1],
                            CultureInfo.InvariantCulture);

                    int volume =
                        Convert.ToInt32(parts[2]);

                    Ticks2.Agents buyer =
                        (Ticks2.Agents)
                        Enum.Parse(
                            typeof(Ticks2.Agents),
                            parts[3]);

                    Ticks2.Agents seller =
                        (Ticks2.Agents)
                        Enum.Parse(
                            typeof(Ticks2.Agents),
                            parts[4]);

                    Ticks2.ActionType starter =
                        (Ticks2.ActionType)
                        Enum.Parse(
                            typeof(Ticks2.ActionType),
                            parts[5]);

                    for (int i = 0; i < missing; i++)
                    {
                        var tick =
                            new Ticks2
                            {
                                TrydID = _nextTrydId++,

                                Time = time,

                                Value = value,

                                Volume = volume,

                                Buyer = buyer,

                                Seller = seller,

                                Starter = starter,

                                Symbol = SYMBOL
                            };
                        Counter++;
                        worker.ReportProgress(GetAverageTicks(Counter, time), tick.ToString());
                        //Console.WriteLine(
                        //    tick.ToString());

                        _pendingTicks.Add(tick);
                    }

                    _sentCount[key] =
                        currentCount;
                }

                var activeKeys =
                    new HashSet<string>(
                        currentBatchCount.Keys);

                var toRemove =
                    _sentCount.Keys
                    .Where(x => !activeKeys.Contains(x))
                    .ToList();

                foreach (var key in toRemove)
                {
                    _sentCount.Remove(key);
                }

                if (_pendingTicks.Count > 0)
                {
                    SendTicks()
                        .GetAwaiter()
                        .GetResult();
                }
            }
        }

        private static async System.Threading.Tasks.Task SendTicks()
        {
            try
            {
                if (_pendingTicks.Count == 0)
                    return;

                if (hubConnection == null ||
                    hubConnection.State != HubConnectionState.Connected)
                {
                    StartHubConnection();
                }

                var arr =
                    _pendingTicks.ToArray();

                await hubConnection.SendAsync(
                    "SendDataTntProfit",
                    arr,
                    SYMBOL);

                Console.WriteLine(
                    $"Sent {arr.Length} ticks");

                _pendingTicks.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
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

            DateTime dt;

            if (!DateTime.TryParse(dat, out dt))
                return false;

            double price;

            if (!double.TryParse(
                pre,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out price))
            {
                return false;
            }

            int qty;

            if (!int.TryParse(
                qul,
                out qty))
            {
                return false;
            }

            if (price <= 0)
                return false;

            if (qty <= 0)
                return false;

            return true;
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
                    return (Ticks2.Agents)
                        Enum.Parse(
                            typeof(Ticks2.Agents),
                            name);
                }
            }
            Console.WriteLine("Corretora não encontrada: " + value);
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
            int row,
            string field)
        {
            foreach (var kv in _topicNames)
            {
                int topicId =
                    kv.Key;

                string name =
                    kv.Value;

                if (name == field + "_" + row)
                {
                    string value;

                    if (_values.TryGetValue(
                        topicId,
                        out value))
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
                    foreach (var topicId in _topicNames.Keys)
                    {
                        try
                        {
                            _rtdServer.DisconnectData(topicId);
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

        public static int GetAverageTicks(int _counter, DateTime _time)
        {
            if (BaseTime == null || BaseTime == default)
            {
                BaseTime = _time;
                return _counter;
            }
            else
            {
                double diff = _time.Subtract(BaseTime).TotalSeconds;
                return (int)(_counter / diff);
            }
        }
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