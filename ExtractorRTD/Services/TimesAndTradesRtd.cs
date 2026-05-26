using B3WM.Shared.Entity;
using ExtractorTryd;
using ExtractorTryd.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExtractorRTD.Services
{
    public static class TimesAndTradesRtd
    {
        public static string ConnectionState
        {
            get
            {
                if (hubConnection == null)
                    return "Disconnected";

                return hubConnection.State.ToString();
            }
        }

        private static IRtdServer _rtdServer;

        private static RtdUpdateEvent _callback;

        private static readonly object _lock =
            new object();

        public static string url { get; set; }

        // HUB
        public static HubConnection hubConnection;

        // MULTIPLOS T&Ts
        private static readonly List<TntConfig> _tnts =
            new List<TntConfig>();

        // QUANTIDADE DE LINHAS
        private const int ROWS = 300;

        // ID SEQUENCIAL
        private static int _nextTrydId = 1;

        // RTD TOPIC ID
        private static int _nextTopicId = 1;

        // DEBUG
        public static int Counter { get; set; }

        public static DateTime BaseTime { get; set; }

        // CACHE DOS VALORES RTD
        private static readonly Dictionary<int, string> _values =
            new Dictionary<int, string>();

        // MAPA:
        // topicId -> "T&T0|DAT|0"
        private static readonly Dictionary<int, string> _topicNames =
            new Dictionary<int, string>();

        // MAPA:
        // "T&T0|DAT|0" -> topicId
        private static readonly Dictionary<string, int> _topicIds =
            new Dictionary<string, int>();

        // CONTROLE DUPLICIDADE
        private static readonly Dictionary<string, int> _sentCount =
            new Dictionary<string, int>();

        // FILA
        private static readonly List<Ticks2> _pendingTicks =
            new List<Ticks2>();

        // EVENTO RTD
        private static readonly AutoResetEvent _dataEvent =
            new AutoResetEvent(false);

        [STAThread]
        public static void Start(
            List<TntConfig> tnts,
            string _url,
            BackgroundWorker worker)
        {
            url = _url;

            _tnts.Clear();

            _tnts.AddRange(tnts);

            worker.ReportProgress(
                0,
                "Starting RTD...");

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

            Console.WriteLine(
                "COM Pointer: " +
                Marshal.GetIUnknownForObject(_rtdServer));

            _callback =
                new RtdUpdateEvent(_dataEvent);

            int result =
                _rtdServer.ServerStart(_callback);

            Console.WriteLine(
                $"ServerStart: {result}");

            StartHubConnection();

            RegisterTopics();

            Console.WriteLine(
                "RTD conectado.");

            while (true)
            {
                try
                {
                    _dataEvent.WaitOne();

                    ReadRtdBatch();

                    ProcessTrades(worker);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
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

                hubConnection =
                    new HubConnectionBuilder()
                    .WithUrl(url)
                    .WithAutomaticReconnect()
                    .Build();

                hubConnection.StartAsync()
                    .GetAwaiter()
                    .GetResult();

                Console.WriteLine(
                    "SignalR conectado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void RegisterTopics()
        {
            foreach (var tnt in _tnts)
            {
                for (int row = 0; row < ROWS; row++)
                {
                    RegisterTopic(
                        tnt.TNTSymbol,
                        row,
                        "DAT");

                    RegisterTopic(
                        tnt.TNTSymbol,
                        row,
                        "PRE");

                    RegisterTopic(
                        tnt.TNTSymbol,
                        row,
                        "QUL");

                    RegisterTopic(
                        tnt.TNTSymbol,
                        row,
                        "ACP");

                    RegisterTopic(
                        tnt.TNTSymbol,
                        row,
                        "AVD");

                    RegisterTopic(
                        tnt.TNTSymbol,
                        row,
                        "AGR");
                }
            }
        }

        private static void RegisterTopic(
            string tnt,
            int row,
            string field)
        {
            int topicId =
                _nextTopicId++;

            Array topic =
                new object[]
                {
                    tnt,
                    field,
                    row.ToString()
                };

            bool newValue = true;

            _rtdServer.ConnectData(
                topicId,
                ref topic,
                ref newValue);

            string key =
                $"{tnt}|{field}|{row}";

            _topicNames[topicId] = key;

            _topicIds[key] = topicId;
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

        private static void ProcessTrades(
            BackgroundWorker worker)
        {
            lock (_lock)
            {
                foreach (var config in _tnts)
                {
                    ProcessTnt(config, worker);
                }

                if (_pendingTicks.Count > 0)
                {
                    SendTicks()
                        .GetAwaiter()
                        .GetResult();
                }
            }
        }

        private static void ProcessTnt(
            TntConfig config,
            BackgroundWorker worker)
        {
            var ticks =
                new List<Ticks2>();

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

                if (!Extensions.TryParsePrice(
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

            ticks =
                ticks
                .OrderBy(x => x.Time)
                .ToList();

            foreach (var tick in ticks)
            {
                string key =
                    config.TNTSymbol + "|" +
                    tick.Time.ToString("HH:mm:ss.fff") + "|" +
                    tick.Value + "|" +
                    tick.Volume + "|" +
                    tick.Buyer + "|" +
                    tick.Seller + "|" +
                    tick.Starter;

                int count = 0;

                if (_sentCount.ContainsKey(key))
                {
                    count =
                        _sentCount[key];
                }

                _sentCount[key] =
                    count + 1;

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
        }

        private static async System.Threading.Tasks.Task SendTicks()
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

                var grouped =
                    _pendingTicks
                    .GroupBy(x => x.Symbol)
                    .ToList();

                foreach (var group in grouped)
                {
                    string symbol =
                        group.Key;

                    var arr =
                        group.ToArray();

                    await hubConnection.SendAsync(
                        "SendDataTntProfit",
                        arr,
                        symbol);

                    Console.WriteLine(
                        $"Sent {arr.Length} ticks [{symbol}]");
                }

                _pendingTicks.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static string GetValue(
            string tnt,
            int row,
            string field)
        {
            string key =
                $"{tnt}|{field}|{row}";

            if (_topicIds.TryGetValue(
                key,
                out int topicId))
            {
                if (_values.TryGetValue(
                    topicId,
                    out string value))
                {
                    return value;
                }
            }

            return "";
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

            if (!DateTime.TryParse(dat, out _))
                return false;

            if (!double.TryParse(
                pre,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
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

            return
                price > 0 &&
                qty > 0;
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
                            _rtdServer.DisconnectData(
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

    public class TntConfig
    {
        public string TNTSymbol { get; set; }

        public string Symbol { get; set; }
    }

    public class RtdUpdateEvent :
        IRTDUpdateEvent
    {
        private readonly AutoResetEvent _event;

        public RtdUpdateEvent(
            AutoResetEvent ev)
        {
            _event = ev;
        }

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
            _event.Set();
        }
    }
}