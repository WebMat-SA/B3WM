using System.Collections.Concurrent;
using B3WM.Services.Core;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services.Screen
{
    /// <summary>
    /// Guarda a última <see cref="MarketWindowRequest"/> de cada conexão
    /// (registrada pelo cliente via <c>SetMarketWindowRequest</c>). É o que
    /// permite o backend empurrar a janela certa (auto/manual conforme a tela)
    /// sem recalcular por broadcast cego.
    /// </summary>
    public sealed class MarketWindowRegistry
    {
        private readonly ConcurrentDictionary<string, MarketWindowRequest> _requests = new();

        public void Set(string connectionId, MarketWindowRequest request) =>
            _requests[connectionId] = request;

        public void Remove(string connectionId) =>
            _requests.TryRemove(connectionId, out _);

        public IReadOnlyList<(string ConnectionId, MarketWindowRequest Request)> ForSymbol(string symbol) =>
            _requests
                .Where(kv => kv.Value.Symbol == symbol)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
    }

    /// <summary>
    /// Empurra a janela vigente para cada conexão inscrita, no fechamento de
    /// candle (debounce por símbolo). Assim o app não precisa do automode local:
    /// o backend é a fonte única e avisa quando a janela muda.
    /// </summary>
    public sealed class MarketWindowBroadcaster : IHostedService, IDisposable
    {
        private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);

        private readonly IEnumerable<OrchestratorService> _orchestrators;
        private readonly IHubContext<DataHub, IDataHubClient> _hub;
        private readonly MarketWindowRegistry _registry;
        private readonly MarketWindowService _service;
        private readonly ILogger<MarketWindowBroadcaster> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _lastPush = new();
        private readonly ConcurrentDictionary<string, string> _lastSignature = new();

        public MarketWindowBroadcaster(
            IEnumerable<OrchestratorService> orchestrators,
            IHubContext<DataHub, IDataHubClient> hub,
            MarketWindowRegistry registry,
            MarketWindowService service,
            ILogger<MarketWindowBroadcaster> logger)
        {
            _orchestrators = orchestrators;
            _hub = hub;
            _registry = registry;
            _service = service;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            foreach (var orchestrator in _orchestrators)
            {
                var symbol = orchestrator.Symbol;
                orchestrator.OnCandleClosed += (_, _) => OnCandleClosedAsync(symbol);
            }
            return Task.CompletedTask;
        }

        private async Task OnCandleClosedAsync(string symbol)
        {
            var now = DateTime.UtcNow;
            if (_lastPush.TryGetValue(symbol, out var last) && now - last < Debounce)
                return;
            _lastPush[symbol] = now;

            foreach (var (connectionId, request) in _registry.ForSymbol(symbol))
            {
                try
                {
                    var window = await _service.ResolveAsync(request);
                    var signature = Signature(window);
                    if (_lastSignature.TryGetValue(connectionId, out var previous) && previous == signature)
                        continue;
                    _lastSignature[connectionId] = signature;
                    await _hub.Clients.Client(connectionId).ReceiveOnMarketWindow(window);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[market-window] push falhou {Symbol} conn={Connection}", symbol, connectionId);
                }
            }
        }

        private static string Signature(MarketWindow window) =>
            $"{window.Intraday.From:O}|{window.Intraday.To:O}|{window.Intraday.Auto}|" +
            $"{window.Daily.From:O}|{window.Daily.To:O}|{window.Daily.Auto}";

        public void Clear(string connectionId)
        {
            _lastSignature.TryRemove(connectionId, out _);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
            _lastPush.Clear();
            _lastSignature.Clear();
        }
    }
}
