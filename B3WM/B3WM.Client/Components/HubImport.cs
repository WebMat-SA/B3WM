using B3WM.Client.Services;
using B3WM.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading.Channels;

namespace B3WM.Client.Components
{
    public class HubImport : IDisposable
    {

        public HubConnection? hubConnection;
        public PeriodicTimer? periodicTimer;
        private CancellationTokenSource cts = new CancellationTokenSource();
        public readonly Channel<string> _channelToDo = Channel.CreateBounded<string>(new BoundedChannelOptions(100000) { SingleReader = true, SingleWriter = false });

        public event EventHandler<string>? Notify;

        private async Task RunTimerAsync()
        {
            try
            {
                while (await periodicTimer.WaitForNextTickAsync(cts.Token))
                {
                    try
                    {

                        if (Notify != null)
                        {
                            while (_channelToDo.Reader.TryRead(out string? _data) )
                            {
                                if (_data == null) continue;

                                Notify.Invoke(this, _data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        HelperPerformanceConfig.Log(nameof(HubImport), nameof(RunTimerAsync), 0, $"{ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation (e.g. when the component is disposed)
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Console.WriteLine($"Timer error: {ex.Message}");
            }
        }


        public async Task<string> Init(string url, int throtlingms = 200)
        {
            try
            {
                HelperPerformanceConfig.Log(nameof(HubImport), "INIT HUB ",
                        0,
                        $"Init {url}");

                //solta o timer
                periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));

                // Start the timer loop in a separate "fire and forget" task
                _ = RunTimerAsync();


                hubConnection = new HubConnectionBuilder()
               .WithUrl(url)
               .WithAutomaticReconnect()
               .Build();

                hubConnection.On<string>(nameof(IDataHubClient.ReceiveCsvLines), OnReceiveTNT);
                //hubConnection.On<byte[]>(nameof(IDataHubClient.ReceiveBook), OnReceiveBook);
                //hubConnection.On<byte[]>(nameof(IDataHubClient.ReceiveTnTSimple), OnReceiveTNTSimple);

                await hubConnection.StartAsync();

                return hubConnection.ConnectionId ?? string.Empty;

            }
            catch (Exception ex)
            {
                HelperPerformanceConfig.Log(nameof(HubImport), "Erro",
                        0,
                        $"Init {ex.Message}");

                return string.Empty;
            }
        }

        private void OnReceiveTNT(string data)
        {

            if (data == null || data.Length == 0) return;

            HelperPerformanceConfig.Log(nameof(HubImport), "HubImportReceived From Server",
                    0,
                    $"Received new data lenght: {data.Length}");

            _ = _channelToDo.Writer.WriteAsync(data);

        }

        public async void Dispose()
        {
            // Stop the timer and cancel the task when the component is disposed
            cts.Cancel();
            periodicTimer?.Dispose();

            if (hubConnection is not null)
            {
                await hubConnection.DisposeAsync();
            }
        }
    }
}
