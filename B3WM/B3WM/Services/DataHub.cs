using B3WM.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services
{
    public class DataHub : Hub<IDataHubClient>
    {
        public async Task SendDataTnT(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                await Clients.All.ReceiveTnT(data);

                var sizeBytes = data.Length;
                var sizeKb = sizeBytes / 1024.0;
                var sizeMb = sizeKb / 1024.0;

                Console.WriteLine($"Message size: {sizeBytes} bytes | {sizeKb:F2} KB | {sizeMb:F4} MB");

                //Console.WriteLine(data.Count());
            }
        }

        public async Task SendDataBook(byte[] data)
        {
            await Clients.All.ReceiveBook(data);
        }

        public async Task SendDataTnTSimple(byte[] data)
        {
            await Clients.All.ReceiveTnTSimple(data);
        }
    }
}
