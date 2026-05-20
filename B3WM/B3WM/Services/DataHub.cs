using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services
{
    public class DataHub : Hub<IDataHubClient>
    {
        private readonly IEnumerable<TickChannelService> _tickChannel;

        public DataHub(IEnumerable<TickChannelService> tickChannel)
        {
            _tickChannel = tickChannel;
        }

        public async Task SendDataTnT(byte[] data, string group)
        {
            if (data != null && data.Length > 0 && !string.IsNullOrEmpty(group))
            {
                await Clients.Group(group).ReceiveTnT(data);

                var sizeBytes = data.Length;
                var sizeKb = sizeBytes / 1024.0;
                var sizeMb = sizeKb / 1024.0;

                Console.WriteLine($"{group} | Message size: {sizeBytes} bytes | {sizeKb:F2} KB | {sizeMb:F4} MB");

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

        public async Task JoinGroup(string group)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }


        public async Task SendDataTntProfit(Ticks2[] data, string group)
        {
            if (data != null && data.Length > 0 && !string.IsNullOrEmpty(group))
            {
                var tickChannel = _tickChannel.FirstOrDefault(tc => tc.Symbol == group);

                if (tickChannel != null)
                {
                    // escreve lote inteiro no channel
                    await tickChannel.Channel.Writer.WriteAsync(data);
                }
                else
                {
                    Console.WriteLine($"Channel for symbol '{group}' not found.");
                }
                //await Clients.Group(group).ReceiveTnTProfit(data);
            }
        }
    }
}
