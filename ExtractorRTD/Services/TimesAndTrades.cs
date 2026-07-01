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

namespace ExtractorRTD.Services
{
    [Obsolete("Substituído pelo TimesAndTradesRtd via COM RTD. Mantido apenas para referência histórica.")]
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
        /// Worker respons�vel por enviar batches para o SignalR
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
                                await hubConnection.SendAsync("SendDataTnT", ms.ToArray(), ativos[0]);
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

                    foreach (var item in ativos)
                    {
                        string str = $@"NEGS$S|{item}#";
                        socket.Send(Encoding.ASCII.GetBytes(str));
                    }

                    // buffer reutiliz�vel
                    byte[] buffer = new byte[1024 * 1024]; // 1MB
                    int bufferLength = 0;

                    byte[] chunk = new byte[8192];

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (hubConnection.State == HubConnectionState.Disconnected)
                            StartHubConnection();

                        int r = socket.Receive(chunk);

                        if (r <= 0)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        Console.WriteLine($"[Socket] Received chunk: {r} bytes");

                        // copia o chunk para o buffer principal
                        Buffer.BlockCopy(chunk, 0, buffer, bufferLength, r);
                        bufferLength += r;

                        Console.WriteLine($"[Buffer] Current size: {bufferLength} bytes");

                        int messageStart = 0;

                        for (int i = 0; i < bufferLength; i++)
                        {
                            if (buffer[i] == (byte)'#')
                            {
                                int messageLength = i - messageStart;

                                if (messageLength > 0)
                                {
                                    byte[] messageBytes = new byte[messageLength];

                                    Buffer.BlockCopy(buffer, messageStart, messageBytes, 0, messageLength);

                                    Console.WriteLine($"[Decode] Message size: {messageLength} bytes");

                                    Decode(messageBytes, worker);
                                }

                                messageStart = i + 1;
                            }
                        }

                        // move o restante n�o processado para o in�cio
                        if (messageStart > 0)
                        {
                            int remaining = bufferLength - messageStart;

                            if (remaining > 0)
                                Buffer.BlockCopy(buffer, messageStart, buffer, 0, remaining);

                            bufferLength = remaining;
                        }

                        Thread.Sleep(1);
                    }

                    SendUnsubscribe(socket);
                }
            }
            catch (Exception expt)
            {
                Console.WriteLine(expt.Message);
            }

            Thread.Sleep(10000);
        }

        public static void Decode(byte[] data, BackgroundWorker worker)
        {
            Counter++;

            _channelToDo.Writer.TryWrite(data);

            string textData = $"Sending {data.Length} bytes ({data.Length / 1024.0:F2} KB)";

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

