using B3WM.Services.Core;

namespace B3WM.Services
{
    public class TickProcessorService : BackgroundService, ISymbolable
    {
        public string Symbol { get; }
        private readonly IEnumerable<TickChannelService> _tickChannel;
        private readonly IEnumerable<OrchestratorService> _orchestratorService;
        private readonly ILogger<TickProcessorService> _logger;

        public TickProcessorService(string symbol, IEnumerable<TickChannelService> tickChannel, IEnumerable<OrchestratorService> orchestrators, ILogger<TickProcessorService> logger)
        {
            this.Symbol = symbol;
            _tickChannel = tickChannel;
            _orchestratorService = orchestrators;
            _logger = logger;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var orchestrator = _orchestratorService.FirstOrDefault(q => q.Symbol == Symbol);
            var tickChannel = _tickChannel.FirstOrDefault(q => q.Symbol == Symbol);

            if (orchestrator == null || tickChannel == null)
            {
                _logger.LogError("TickProcessorService for symbol {Symbol} could not find matching OrchestratorService or TickChannelService.", Symbol);
                return;
            }

            await foreach (var batch in tickChannel.Channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await orchestrator.Enqueue(batch);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TickProcessorService.Enqueue error");
                }
            }
        }
    }
}
