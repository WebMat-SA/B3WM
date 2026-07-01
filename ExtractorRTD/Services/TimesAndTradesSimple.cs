using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExtractorRTD.Services
{
    [Obsolete("Substituído pelo TimesAndTradesRtd via COM RTD. Mantido apenas para referência histórica.")]
    public class TimesAndTradesSimple
    {
        public static HubConnection hubConnection;
        public static int Counter { get; set; } = 0;


        public static string[] ativos { get; set; } = new string[] { "WINJ26" };

        // Deduplication: remember recent message hashes and timestamps to avoid processing duplicates
        private static readonly object _dedupLock = new object();
        private static readonly Dictionary<string, DateTime> _recentHashes = new Dictionary<string, DateTime>();
        // window to consider a message duplicate (tune as needed)
        private static readonly TimeSpan _dedupWindow = TimeSpan.FromSeconds(5);

        public static void StartHubConnection()
        {
            if (hubConnection != null)
            {
                try { hubConnection.StopAsync().GetAwaiter().GetResult(); } catch { }
                hubConnection.DisposeAsync().AsTask().GetAwaiter().GetResult();
                hubConnection = null;
            }
            hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:5002/api/datahub")
                .Build();
            hubConnection.StartAsync();
        }

        /// <summary>Envia comando de unsubscribe ao RTD para evitar assinatura duplicada ao reconectar.</summary>
        private static void SendUnsubscribe(Socket socket)
        {
            if (socket == null || !socket.Connected) return;
            try
            {
                foreach (var item in ativos)
                {
                    string str = $@"NEG$U|{item}#";
                    socket.Send(Encoding.ASCII.GetBytes(str));
                }
            }
            catch { }
        }

        public static void Start(CancellationToken stoppingToken, BackgroundWorker worker, string[] _ativos)
        {
            try
            {
                ativos = _ativos;

                StartHubConnection();

                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(Dns.GetHostAddresses("127.0.0.1"), 12002);

                    //Console.Write("Initializing list... ");

                    foreach (var item in ativos)
                    {

                        string str = $@"NEG$S|{item}#";
                        socket.Send(Encoding.ASCII.GetBytes(str));
                    }

                    byte[] data = new byte[0];

                    //Console.WriteLine("Finalizing list...");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (stoppingToken.IsCancellationRequested) { SendUnsubscribe(socket); break; }
                        if (hubConnection.State == HubConnectionState.Disconnected) StartHubConnection();

                        byte[] chunk = new byte[8192];

                        int r = socket.Receive(chunk);

                        if (r <= 0)
                        {
                            Task.Delay(250).Wait();
                            continue;
                        }

                        // append only the received bytes
                        if (r < chunk.Length)
                        {
                            var received = new byte[r];
                            Array.Copy(chunk, 0, received, 0, r);
                            data = data.Concat(received).ToArray();
                        }
                        else
                        {
                            data = data.Concat(chunk).ToArray();
                        }

                        // process all complete messages delimited by '#'
                        int indexSharp;
                        while ((indexSharp = Array.IndexOf(data, (byte)35)) >= 0)
                        {
                            var messageBytes = new byte[indexSharp];
                            Array.Copy(data, 0, messageBytes, 0, indexSharp);

                            // remove processed part and the '#'
                            var remaining = new byte[data.Length - indexSharp - 1];
                            if (remaining.Length > 0)
                                Array.Copy(data, indexSharp + 1, remaining, 0, remaining.Length);
                            data = remaining;

                            Decode(messageBytes, worker);
                        }

                        Task.Delay(250).Wait();
                    }
                }
            }
            catch (Exception expt)
            {
                Console.WriteLine(expt.Message);
            }

            Task.Delay(10000).Wait();
        }

        public static void Decode(byte[] data, BackgroundWorker worker)
        {
            string textData = Encoding.UTF8.GetString(data);

            //// compute hash of payload to compare messages efficiently
            //string hash;
            //using (var sha = SHA256.Create())
            //{
            //    var hashed = sha.ComputeHash(data ?? new byte[0]);
            //    hash = Convert.ToBase64String(hashed);
            //}

            //var now = DateTime.UtcNow;
            //lock (_dedupLock)
            //{
            //    // remove old entries
            //    var oldKeys = new List<string>();
            //    foreach (var kvp in _recentHashes)
            //    {
            //        if ((now - kvp.Value) > _dedupWindow) oldKeys.Add(kvp.Key);
            //    }
            //    foreach (var k in oldKeys) _recentHashes.Remove(k);

            //    // if we've seen this hash recently, ignore
            //    if (_recentHashes.TryGetValue(hash, out var lastSeen) && (now - lastSeen) <= _dedupWindow)
            //    {
            //        return; // duplicate within dedup window
            //    }

            //    // record this hash
            //    _recentHashes[hash] = now;
            //}

            Counter++;

            //envia para o servidor via signalR
            hubConnection.SendAsync("SendDataTnTSimple", data);
            worker.ReportProgress(Counter, textData);

        }
        //}
    }
}
