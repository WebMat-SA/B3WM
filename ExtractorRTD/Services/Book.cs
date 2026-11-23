using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ExtractorRTD.Services
{
    /// <summary>
    /// Coleta o livro LVL2 do Profit pelo socket RTD local (127.0.0.1:12002)
    /// e envia snapshots agregados por preço para o servidor B3WM via SignalR.
    ///
    /// Protocolo (Profit/Tryd):
    ///   envio : LVL2$S|tipo_book|ativo|linha|coluna#
    ///   retorno: LVL2!ativo|a;b;coluna;valor|a;b;coluna;valor|...#
    ///
    /// tipo_book = 1 (agrupado por preço). Colunas:
    ///   0 = oferta/agente compra, 1 = qtd compra, 2 = preço compra,
    ///   3 = preço venda, 4 = qtd venda, 5 = oferta/agente venda.
    /// </summary>
    public static class BookLvl2
    {
        private const string Host = "127.0.0.1";
        private const int Port = 12002;
        private const int BookType = 1;
        private const int Rows = 30;
        private const int Cols = 6;
        private const int MaxLevels = 30;
        private const int ReceiveTimeoutMs = 250;
        private const int PublishIntervalMs = 300;
        private const int LevelTtlMs = 8000;
        private const int HubRetryMs = 5000;

        private static readonly object Sync = new object();

        private static readonly Dictionary<string, SymbolBook> Books =
            new Dictionary<string, SymbolBook>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> Roots =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "WINFUT", "WIN" },
                { "INDFUT", "IND" },
                { "WDOFUT", "WDO" },
                { "DOLFUT", "DOL" },
            };

        private static readonly string[] MonthCodes =
        {
            "F", "G", "H", "J", "K", "M",
            "N", "Q", "U", "V", "X", "Z"
        };

        private static HubConnection _hub;
        private static string _url;
        private static CancellationToken _token;
        private static DateTime _lastHubAttempt = DateTime.MinValue;
        private static readonly HashSet<string> LoggedSamples =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string ConnectionState =>
            _hub?.State.ToString() ?? "Disconnected";

        public static void Start(
            IEnumerable<string> symbols,
            string url,
            BackgroundWorker worker,
            CancellationToken token)
        {
            _url = url;
            _token = token;

            var canonical = (symbols ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            var contracts = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            lock (Sync)
            {
                Books.Clear();
                LoggedSamples.Clear();
                foreach (var symbol in canonical)
                {
                    var contract = ResolveActiveContract(symbol);
                    contracts[symbol] = contract;
                    Books[symbol] = new SymbolBook(symbol, contract);
                }
            }

            Report(worker, "BOOK",
                "LVL2 start: " + string.Join(", ",
                    canonical.Select(s => s + "->" + contracts[s])));

            _lastHubAttempt = DateTime.MinValue;
            EnsureHub(worker);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    RunSocketLoop(contracts, worker, token);
                }
                catch (Exception ex)
                {
                    Report(worker, "BOOK", "LVL2 erro: " + ex.Message);
                }

                if (!token.IsCancellationRequested)
                    Thread.Sleep(2000);
            }

            Stop();
        }

        public static void Stop()
        {
            try { _hub?.StopAsync().Wait(1000); } catch { }
        }

        private static void RunSocketLoop(
            Dictionary<string, string> contracts,
            BackgroundWorker worker,
            CancellationToken token)
        {
            using (var socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp))
            {
                socket.ReceiveTimeout = ReceiveTimeoutMs;
                socket.Connect(ResolveHost(), Port);
                Report(worker, "BOOK",
                    "LVL2 conectado em " + Host + ":" + Port);

                Subscribe(socket, contracts.Values);

                var buffer = new List<byte>(8192);
                var lastPublish = DateTime.MinValue;
                var lastStats = DateTime.MinValue;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        EnsureHub(worker);

                        var chunk = new byte[8192];
                        var read = 0;
                        try
                        {
                            read = socket.Receive(chunk);
                        }
                        catch (SocketException ex)
                            when (ex.SocketErrorCode == SocketError.TimedOut ||
                                  ex.SocketErrorCode == SocketError.WouldBlock)
                        {
                            read = 0;
                        }

                        if (read > 0)
                        {
                            for (var i = 0; i < read; i++)
                                buffer.Add(chunk[i]);
                            ProcessBuffer(buffer, worker);
                        }
                        else if (read == 0)
                        {
                            // peer closed
                            if (!token.IsCancellationRequested)
                                throw new SocketException((int)SocketError.ConnectionReset);
                        }

                        var now = DateTime.UtcNow;
                        TryFallbackSubscriptions(socket, worker, now);
                        if ((now - lastPublish).TotalMilliseconds >=
                            PublishIntervalMs)
                        {
                            Publish(worker);
                            lastPublish = now;
                        }

                        if ((now - lastStats).TotalSeconds >= 5)
                        {
                            LogStats(worker);
                            lastStats = now;
                        }
                    }
                }
                finally
                {
                    try { Unsubscribe(socket, contracts.Values); } catch { }
                }
            }
        }

        private static void EnsureHub(BackgroundWorker worker)
        {
            if (_hub != null &&
                _hub.State != HubConnectionState.Disconnected)
                return;

            if ((DateTime.UtcNow - _lastHubAttempt).TotalMilliseconds < HubRetryMs)
                return;

            _lastHubAttempt = DateTime.UtcNow;

            try
            {
                try { _hub?.DisposeAsync().AsTask().Wait(500); } catch { }
                _hub = null;

                _hub = new HubConnectionBuilder()
                    .WithUrl(_url)
                    .WithAutomaticReconnect()
                    .Build();

                _hub.StartAsync().Wait(4000);
                Report(worker, "BOOK", "LVL2 SignalR: " + _hub.State);
            }
            catch (Exception ex)
            {
                Report(worker, "BOOK",
                    "LVL2 SignalR falhou: " + ex.Message);
            }
        }

        private static void Subscribe(Socket socket, IEnumerable<string> contracts)
        {
            foreach (var contract in contracts.Distinct(StringComparer.OrdinalIgnoreCase))
                SubscribeOne(socket, contract);
        }

        private static void SubscribeOne(Socket socket, string contract)
        {
            for (var row = 0; row < Rows; row++)
            {
                for (var col = 0; col < Cols; col++)
                {
                    SendCommand(socket, "LVL2$S|" + BookType + "|" + contract +
                                        "|" + row + "|" + col + "#");
                }
            }
        }

        private static void Unsubscribe(Socket socket, IEnumerable<string> contracts)
        {
            foreach (var contract in contracts.Distinct(StringComparer.OrdinalIgnoreCase))
                UnsubscribeOne(socket, contract);
        }

        private static void UnsubscribeOne(Socket socket, string contract)
        {
            for (var row = 0; row < Rows; row++)
            {
                for (var col = 0; col < Cols; col++)
                {
                    SendCommand(socket, "LVL2$U|" + BookType + "|" + contract +
                                        "|" + row + "|" + col + "#");
                }
            }
        }

        private static void SendCommand(Socket socket, string command)
        {
            var bytes = Encoding.ASCII.GetBytes(command);
            var sent = 0;
            while (sent < bytes.Length)
            {
                sent += socket.Send(
                    bytes, sent, bytes.Length - sent, SocketFlags.None);
            }
        }

        /// <summary>
        /// Se o contrato resolvido não trouxer nenhum nível em alguns segundos,
        /// tenta o nome contínuo (ex.: WINZ26 -> WINFUT), que algumas versões
        /// do Profit aceitam no socket LVL2.
        /// </summary>
        private static void TryFallbackSubscriptions(
            Socket socket,
            BackgroundWorker worker,
            DateTime now)
        {
            List<SymbolBook> fallback = null;
            lock (Sync)
            {
                foreach (var book in Books.Values)
                {
                    if (book.EverHadData || book.TriedCanonical)
                        continue;
                    if ((now - book.SubscribedAt).TotalSeconds < 8)
                        continue;

                    book.TriedCanonical = true;
                    if (fallback == null)
                        fallback = new List<SymbolBook>();
                    fallback.Add(book);
                }
            }

            if (fallback == null)
                return;

            foreach (var book in fallback)
            {
                try
                {
                    UnsubscribeOne(socket, book.Contract);
                    SubscribeOne(socket, book.Canonical);
                    Report(worker, book.Canonical,
                        "LVL2 sem dados em " + book.Contract +
                        "; tentando " + book.Canonical);
                }
                catch (Exception ex)
                {
                    Report(worker, book.Canonical,
                        "LVL2 fallback falhou: " + ex.Message);
                }
            }
        }

        private static void ProcessBuffer(List<byte> buffer, BackgroundWorker worker)
        {
            int index;
            while ((index = buffer.IndexOf((byte)'#')) >= 0)
            {
                var messageBytes = new byte[index];
                buffer.CopyTo(0, messageBytes, 0, index);
                buffer.RemoveRange(0, index + 1);

                if (messageBytes.Length == 0)
                    continue;

                var message = Encoding.UTF8.GetString(messageBytes);
                try
                {
                    Decode(message, worker);
                }
                catch (Exception ex)
                {
                    Report(worker, "BOOK", "LVL2 parse: " + ex.Message);
                }
            }

            if (buffer.Count > 1024 * 1024)
                buffer.Clear();
        }

        private static void Decode(string message, BackgroundWorker worker)
        {
            var parts = message.Split('|');
            if (parts.Length < 2)
                return;

            var prefix = parts[0];
            if (!prefix.StartsWith("LVL2!", StringComparison.OrdinalIgnoreCase))
                return;

            var paper = prefix.Substring(5).Trim();
            var canonical = ResolveCanonical(paper);
            if (canonical == null)
                return;

            SymbolBook book;
            lock (Sync)
            {
                if (!Books.TryGetValue(canonical, out book))
                    return;

                if (LoggedSamples.Add(canonical))
                {
                    Report(worker, canonical,
                        "LVL2 sample: " + Truncate(message, 400));
                }
            }

            int? buyQty = null;
            double? sellPrice = null;
            int? sellQty = null;
            var now = DateTime.UtcNow;

            for (var i = 1; i < parts.Length; i++)
            {
                var seg = parts[i].Split(';');
                if (seg.Length != 4)
                    continue;

                if (!int.TryParse(seg[2], out var col))
                    continue;

                var content = seg[3];

                switch (col)
                {
                    case 1:
                        if (TryParseQty(content, out var q1))
                            buyQty = q1;
                        break;

                    case 2:
                        if (TryParsePrice(content, out var p2) &&
                            buyQty.HasValue)
                        {
                            lock (Sync)
                                book.SetBid(p2, buyQty.Value, now);
                            buyQty = null;
                        }
                        break;

                    case 3:
                        if (TryParsePrice(content, out var p3))
                            sellPrice = p3;
                        break;

                    case 4:
                        if (TryParseQty(content, out var q4))
                            sellQty = q4;
                        break;

                    case 5:
                        if (sellPrice.HasValue && sellQty.HasValue)
                        {
                            lock (Sync)
                                book.SetAsk(sellPrice.Value, sellQty.Value, now);
                            sellPrice = null;
                            sellQty = null;
                        }
                        break;
                }
            }
        }

        private static void Publish(BackgroundWorker worker)
        {
            if (_token.IsCancellationRequested)
                return;

            var hub = _hub;
            if (hub == null || hub.State != HubConnectionState.Connected)
                return;

            List<BookSnapshotDto> snapshots;
            var now = DateTime.UtcNow;

            lock (Sync)
            {
                foreach (var book in Books.Values)
                    book.Prune(now, TimeSpan.FromMilliseconds(LevelTtlMs));

                snapshots = Books.Values
                    .Where(b => b.HasData)
                    .Select(b => b.ToSnapshot(now))
                    .ToList();
            }

            foreach (var snapshot in snapshots)
            {
                try
                {
                    if (!hub.SendAsync("SendDataBookSnapshot", snapshot)
                        .Wait(1500))
                    {
                        Report(worker, snapshot.Symbol,
                            "LVL2 envio: timeout");
                    }
                }
                catch (Exception ex)
                {
                    Report(worker, snapshot.Symbol,
                        "LVL2 envio falhou: " + ex.Message);
                }
            }
        }

        private static void LogStats(BackgroundWorker worker)
        {
            List<string> lines;
            lock (Sync)
            {
                lines = Books.Values
                    .Select(b => b.Canonical + ": " +
                                 b.BidCount + " bids / " +
                                 b.AskCount + " asks")
                    .ToList();
            }

            foreach (var line in lines)
                Report(worker, "BOOK", "LVL2 " + line);
        }

        private static string ResolveHost()
        {
            try
            {
                var addresses = Dns.GetHostAddresses(Host);
                return addresses.Length > 0
                    ? addresses[0].ToString()
                    : Host;
            }
            catch
            {
                return Host;
            }
        }

        private static string ResolveCanonical(string paper)
        {
            var value = (paper ?? "").Trim().ToUpperInvariant();
            if (value.Length == 0)
                return null;

            foreach (var kv in Roots)
            {
                if (value == kv.Key ||
                    value.StartsWith(kv.Value, StringComparison.Ordinal))
                    return kv.Key;
            }

            return null;
        }

        private static string ResolveActiveContract(string symbol)
        {
            var value = (symbol ?? "").Trim().ToUpperInvariant();
            if (!Roots.TryGetValue(value, out var root))
                return value; // já é um contrato específico

            var today = DateTime.Today;
            var year = today.Year;
            var month = today.Month;

            if (root == "WDO" || root == "DOL")
            {
                var expiry = FirstBusinessDay(year, month);
                if (today > expiry)
                {
                    month++;
                    if (month > 12)
                    {
                        month = 1;
                        year++;
                    }
                }
            }
            else
            {
                if (month % 2 != 0)
                {
                    month++;
                    if (month > 12)
                    {
                        month = 2;
                        year++;
                    }
                }
                else
                {
                    var expiry = WednesdayNear15(year, month);
                    if (today > expiry)
                    {
                        month += 2;
                        if (month > 12)
                        {
                            month = 2;
                            year++;
                        }
                    }
                }
            }

            return root + MonthCodes[month - 1] +
                   (year % 100).ToString("00");
        }

        private static DateTime FirstBusinessDay(int year, int month)
        {
            var date = new DateTime(year, month, 1);
            while (date.DayOfWeek == DayOfWeek.Saturday ||
                   date.DayOfWeek == DayOfWeek.Sunday)
                date = date.AddDays(1);
            return date;
        }

        private static DateTime WednesdayNear15(int year, int month)
        {
            var date = new DateTime(year, month, 15);
            var diff = ((int)DayOfWeek.Wednesday -
                        (int)date.DayOfWeek + 7) % 7;
            if (diff > 3)
                diff -= 7;
            return date.AddDays(diff);
        }

        private static bool TryParsePrice(string value, out double price)
        {
            return Extensions.TryParsePrice(value, out price);
        }

        private static bool TryParseQty(string value, out int qty)
        {
            qty = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (int.TryParse(value, NumberStyles.Any,
                    CultureInfo.GetCultureInfo("pt-BR"), out qty))
                return true;

            if (int.TryParse(value, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out qty))
                return true;

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length > 0 && int.TryParse(digits, out qty);
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max) + "...";
        }

        private static void Report(
            BackgroundWorker worker,
            string symbol,
            string message)
        {
            try
            {
                worker?.ReportProgress(0, new TickLogItem
                {
                    Symbol = symbol,
                    Message = message,
                });
            }
            catch
            {
            }
        }

        private sealed class LevelState
        {
            public LevelState(double volume, DateTime touchedAt)
            {
                Volume = volume;
                TouchedAt = touchedAt;
            }

            public double Volume { get; }

            public DateTime TouchedAt { get; }
        }

        private sealed class SymbolBook
        {
            private readonly Dictionary<double, LevelState> _bids =
                new Dictionary<double, LevelState>();

            private readonly Dictionary<double, LevelState> _asks =
                new Dictionary<double, LevelState>();

            public SymbolBook(string canonical, string contract)
            {
                Canonical = canonical;
                Contract = contract;
            }

            public string Canonical { get; }

            public string Contract { get; }

            public DateTime SubscribedAt { get; } = DateTime.UtcNow;

            public bool TriedCanonical { get; set; }

            public bool EverHadData { get; private set; }

            public int BidCount => _bids.Count;

            public int AskCount => _asks.Count;

            public bool HasData => _bids.Count > 0 || _asks.Count > 0;

            public void SetBid(double price, double volume, DateTime now)
            {
                if (volume > 0)
                    EverHadData = true;
                SetLevel(_bids, price, volume, now);
            }

            public void SetAsk(double price, double volume, DateTime now)
            {
                if (volume > 0)
                    EverHadData = true;
                SetLevel(_asks, price, volume, now);
            }

            public void Prune(DateTime now, TimeSpan ttl)
            {
                PruneDictionary(_bids, now, ttl);
                PruneDictionary(_asks, now, ttl);
            }

            public BookSnapshotDto ToSnapshot(DateTime now)
            {
                return new BookSnapshotDto
                {
                    Symbol = Canonical,
                    Time = now,
                    Bids = ToLevels(_bids, descending: true),
                    Asks = ToLevels(_asks, descending: false),
                    Available = HasData,
                    Source = "rtd_lvl2",
                    Message = HasData
                        ? ""
                        : "LVL2 sem ofertas para " + Contract,
                };
            }

            private static void SetLevel(
                Dictionary<double, LevelState> levels,
                double price,
                double volume,
                DateTime now)
            {
                if (double.IsNaN(price) || double.IsInfinity(price) || price <= 0)
                    return;

                var key = Math.Round(price, 4);
                if (volume <= 0)
                {
                    levels.Remove(key);
                    return;
                }

                levels[key] = new LevelState(volume, now);
            }

            private static void PruneDictionary(
                Dictionary<double, LevelState> levels,
                DateTime now,
                TimeSpan ttl)
            {
                var stale = levels
                    .Where(kv => now - kv.Value.TouchedAt > ttl)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in stale)
                    levels.Remove(key);
            }

            private static List<BookLevelDto> ToLevels(
                Dictionary<double, LevelState> levels,
                bool descending)
            {
                var ordered = descending
                    ? levels.OrderByDescending(kv => kv.Key)
                    : levels.OrderBy(kv => kv.Key);

                return ordered
                    .Take(MaxLevels)
                    .Select(kv => new BookLevelDto
                    {
                        Price = kv.Key,
                        Volume = kv.Value.Volume,
                    })
                    .ToList();
            }
        }
    }

    public sealed class BookLevelDto
    {
        public double Price { get; set; }

        public double Volume { get; set; }
    }

    public sealed class BookSnapshotDto
    {
        public string Symbol { get; set; } = "";

        public DateTime Time { get; set; } = DateTime.UtcNow;

        public List<BookLevelDto> Bids { get; set; } = new List<BookLevelDto>();

        public List<BookLevelDto> Asks { get; set; } = new List<BookLevelDto>();

        public bool Available { get; set; } = true;

        public string Source { get; set; } = "rtd_lvl2";

        public string Message { get; set; } = "";
    }
}
