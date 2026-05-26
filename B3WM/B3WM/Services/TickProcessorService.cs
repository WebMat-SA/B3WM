using B3WM.Services.Core;

namespace B3WM.Services
{
    public class TickProcessorService : BackgroundService, ISymbolable
    {
        public string Symbol { get; }
        private readonly IEnumerable<TickChannelService> _tickChannel;
        private readonly IEnumerable<OrchestratorService> _orchestratorService;

        public TickProcessorService(string symbol, IEnumerable<TickChannelService> tickChannel, IEnumerable<OrchestratorService> orchestrators)
        {
            this.Symbol = symbol;
            _tickChannel = tickChannel;
            _orchestratorService = orchestrators;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var orchestrator = _orchestratorService.FirstOrDefault(q => q.Symbol == Symbol);
            var tickChannel = _tickChannel.FirstOrDefault(q => q.Symbol == Symbol);

            if (orchestrator == null || tickChannel == null)
            {
                Console.WriteLine($"TickProcessorService for symbol {Symbol} could not find matching OrchestratorService or TickChannelService.");
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
                    Console.WriteLine(ex.Message);
                }
            }
        }
    }
}
