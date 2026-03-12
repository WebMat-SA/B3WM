using B3WM.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text;
using System.Threading.Channels;

namespace B3WM.Client.Services
{
    public class HubClientWorker : IDisposable
    {
        public event EventHandler<IEnumerable<byte[]>>? Notify;

        public HubConnection? hubConnection;
        public PeriodicTimer? periodicTimer;
        private CancellationTokenSource cts = new CancellationTokenSource();
        public readonly Channel<byte[]> _channelToDo = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1000) { SingleReader = true, SingleWriter = false });

        private async Task RunTimerAsync()
        {
            try
            {
                while (await periodicTimer.WaitForNextTickAsync(cts.Token))
                {
                    if (Notify != null)
                    {
                        var list = new List<byte[]>();

                        while (_channelToDo.Reader.TryRead(out byte[]? _data)) 
                        {
                            if (_data == null) continue;

                            list.Add(_data); 
                        }

                        Notify.Invoke(this, list);
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


        public async void Init(string url, int throtlingms = 200)
        {
            try
            {
                HelperPerformanceConfig.Log(nameof(HubClientWorker), "INIT HUB ",
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

                hubConnection.On<byte[]>(nameof(IDataHubClient.ReceiveTnT), OnReceiveTNT);
                //hubConnection.On<byte[]>(nameof(IDataHubClient.ReceiveBook), OnReceiveBook);
                //hubConnection.On<byte[]>(nameof(IDataHubClient.ReceiveTnTSimple), OnReceiveTNTSimple);

                await hubConnection.StartAsync();

            }
            catch (Exception ex)
            {
                HelperPerformanceConfig.Log(nameof(HubClientWorker), "Erro",
                        0,
                        $"Init {ex.Message}");
            }
        }

        private void OnReceiveTNT(byte[] data)
        {

            if (data == null || data.Length == 0) return;

            HelperPerformanceConfig.Log(nameof(HubClientWorker), "OnReceiveTNT",
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
