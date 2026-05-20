using B3WM.Services.Core;
using B3WM.Shared.Entity;
using System.Threading.Channels;

namespace B3WM.Services
{
    public class TickChannelService : ISymbolable
    {
        public string Symbol { get; }

        public TickChannelService(string symbol)
        {
            Symbol = symbol;
        }

        public Channel<Ticks2[]> Channel{ get; } =
            System.Threading.Channels.Channel.CreateUnbounded<Ticks2[]>();
    }
}
