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

        public static readonly Channel<byte[]> _channelToDo =
            Channel.CreateBounded<byte[]>(new BoundedChannelOptions(5000)
            {
                SingleReader = true,
                SingleWriter = true
            });

        public static void StartHubConnection()
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:5001/api/datahub")
                .Build();

            hubConnection.StartAsync().Wait();
        }

        /// <summary>
        /// Worker responsável por enviar batches para o SignalR
        /// </summary>
        public static async Task WorkChannel()
        {
            while (await _channelToDo.Reader.WaitToReadAsync())
            {
                using (var ms = new MemoryStream())
                {
                    while (_channelToDo.Reader.TryRead(out var data))
                    {
                        ms.Write(data, 0, data.Length);
                    }

                    if (hubConnection != null &&
                        hubConnection.State == HubConnectionState.Connected)
                    {
                        await hubConnection.SendAsync("SendDataTnT", ms.ToArray());
                    }
                }
            }
        }

        public static void Start(CancellationToken stoppingToken, BackgroundWorker worker, string[] _ativos)
        {
            try
            {
                ativos = _ativos;

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

                    MemoryStream buffer = new MemoryStream();
                    byte[] readBuffer = new byte[8192];

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (hubConnection.State == HubConnectionState.Disconnected)
                            StartHubConnection();

                        int bytesRead = socket.Receive(readBuffer);

                        if (bytesRead <= 0)
                            continue;

                        buffer.Write(readBuffer, 0, bytesRead);

                        ProcessBuffer(buffer, worker);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Processa o buffer procurando delimitador '#'
        /// </summary>
        private static void ProcessBuffer(MemoryStream buffer, BackgroundWorker worker)
        {
            byte[] data = buffer.ToArray();

            int start = 0;

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == (byte)'#')
                {
                    int length = i - start;

                    if (length > 0)
                    {
                        byte[] message = new byte[length];
                        Buffer.BlockCopy(data, start, message, 0, length);

                        Decode(message, worker);
                    }

                    start = i + 1;
                }
            }

            if (start > 0)
            {
                buffer.SetLength(0);
                buffer.Write(data, start, data.Length - start);
            }
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

