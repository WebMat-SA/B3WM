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
