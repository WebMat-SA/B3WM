namespace B3WM.Services
{
    public class TickProcessorService : BackgroundService
    {
        private readonly TickChannelService _tickChannel;

        public TickProcessorService(TickChannelService tickChannel)
        {
            _tickChannel = tickChannel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var batch in _tickChannel.Channel.Reader.ReadAllAsync(stoppingToken))
            {
                foreach (var tick in batch)
                {
                    Console.WriteLine("Service: " +
                        System.Text.Json.JsonSerializer.Serialize(tick)
                    );

                    // processa tick aqui
                }
            }
        }
    }
}
