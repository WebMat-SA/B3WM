using B3WM.Services.Screen;
using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services
{
    public class DataHub : Hub<IDataHubClient>
    {
        private readonly IEnumerable<TickChannelService> _tickChannel;
        private readonly MarketWindowRegistry _marketWindowRegistry;
        private readonly MarketWindowService _marketWindowService;
        private readonly MarketWindowBroadcaster _marketWindowBroadcaster;
        private readonly ILogger<DataHub> _logger;

        public DataHub(IEnumerable<TickChannelService> tickChannel, MarketWindowRegistry marketWindowRegistry, MarketWindowService marketWindowService, MarketWindowBroadcaster marketWindowBroadcaster, ILogger<DataHub> logger)
        {
            _tickChannel = tickChannel;
            _marketWindowRegistry = marketWindowRegistry;
            _marketWindowService = marketWindowService;
            _marketWindowBroadcaster = marketWindowBroadcaster;
            _logger = logger;
        }

        public async Task JoinGroup(string group)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        /// <summary>
        /// Registra o pedido de janela da conexão (auto/manual da tela) e
        /// devolve a janela vigente; o backend passa a empurrar
        /// <c>ReceiveOnMarketWindow</c> no fechamento de candle.
        /// </summary>
        public async Task<MarketWindow?> SetMarketWindowRequest(MarketWindowRequest request)
        {
            if (request is null || string.IsNullOrEmpty(request.Symbol)) return null;
            _marketWindowRegistry.Set(Context.ConnectionId, request);
            await Groups.AddToGroupAsync(Context.ConnectionId, request.Symbol);
            return await _marketWindowService.ResolveAsync(request);
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _marketWindowRegistry.Remove(Context.ConnectionId);
            _marketWindowBroadcaster.Clear(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
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
                    _logger.LogWarning("Channel for symbol '{Group}' not found.", group);
                }
                //await Clients.Group(group).ReceiveTnTProfit(data);
            }
        }
    }
}
