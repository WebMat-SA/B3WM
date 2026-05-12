using B3WM.Shared.Entity;
using System.Threading.Channels;

namespace B3WM.Services
{
    public class TickChannelService
    {
        public Channel<Ticks2[]> Channel { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<Ticks2[]>();
    }
}
