using B3WM.Services.Core;

namespace B3WM.Services
{
    public class TickProcessorService : BackgroundService
    {
        private readonly TickChannelService _tickChannel;
        private readonly OrchestratorService _orchestratorService;

        public TickProcessorService(TickChannelService tickChannel, OrchestratorService orchestrator)
        {
            _tickChannel = tickChannel;
            _orchestratorService = orchestrator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var batch in _tickChannel.Channel.Reader.ReadAllAsync(stoppingToken))
            {
                await _orchestratorService.Enqueue(batch);
            }
        }
    }
}
