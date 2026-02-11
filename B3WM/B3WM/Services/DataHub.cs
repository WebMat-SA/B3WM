using B3WM.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services
{
    public class DataHub : Hub<IDataHubClient>
    {
        public async Task SendDataTnT(byte[] data)
        {
            await Clients.All.ReceiveTnT(data);
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
