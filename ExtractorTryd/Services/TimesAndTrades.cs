using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace ExtractorTryd.Services
{
    public class TimesAndTrades
    {
        public static HubConnection hubConnection;
        public static int Counter { get; set; } = 0;


        public static string[] ativos { get; set; } = new string[] { "WINJ26" };

        public static string url { get; set; }

        public static readonly Channel<byte[]> _channelToDo =
            Channel.CreateBounded<byte[]>(new BoundedChannelOptions(5000)
            {
                SingleReader = true,
                SingleWriter = true
            });

        public static void StartHubConnection()
        {
            if (hubConnection != null) hubConnection.DisposeAsync();

            hubConnection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();

            hubConnection.StartAsync().Wait();
        }

        /// <summary>
        /// Worker responsável por enviar batches para o SignalR
        /// </summary>
        public static async Task WorkChannel()
        {
            while (true)
            {
                StartHubConnection();

                try
                {
                    while (await _channelToDo.Reader.WaitToReadAsync())
                    {
                        int count = 0;

                        using (var ms = new MemoryStream())
                        {
                            while (_channelToDo.Reader.TryRead(out var data) && count < 10)
                            {
                                ms.Write(data, 0, data.Length);
                                count++;
                            }

                            if (hubConnection != null &&
                                hubConnection.State == HubConnectionState.Connected)
                            {
                                await hubConnection.SendAsync("SendDataTnT", ms.ToArray());
                                await Task.Delay(250);
                            }
                            else
                            {
                                StartHubConnection();
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public static void Start(CancellationToken stoppingToken, BackgroundWorker worker, string[] _ativos, string _url)
        {
            try
            {
                ativos = _ativos;
                url = _url;

                StartHubConnection();

                _ = WorkChannel();

                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(Dns.GetHostAddresses("127.0.0.1"), 12002);

                    //Console.Write("Initializing list... ");

                    foreach (var item in ativos)
                    {

                        string str = $@"NEGS$S|{item}#";
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
            Counter++;

            _channelToDo.Writer.TryWrite(data);

            string textData = Encoding.UTF8.GetString(data);

            worker.ReportProgress(Counter, textData);
        }

        /// <summary>
        /// Envia unsubscribe
        /// </summary>
        private static void SendUnsubscribe(Socket socket)
        {
            if (socket == null || !socket.Connected) return;

            try
            {
                foreach (var item in ativos)
                {
                    string str = $@"NEGS$U|{item}#";
                    socket.Send(Encoding.ASCII.GetBytes(str));
                }
            }
            catch { }
        }
    }
}

