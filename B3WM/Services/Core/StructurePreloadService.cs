using Microsoft.Extensions.Hosting;

namespace B3WM.Services.Core
{
    public class StructurePreloadService : IHostedService
    {
        private readonly IEnumerable<StructureService> _structureServices;

        public StructurePreloadService(IEnumerable<StructureService> structureServices)
        {
            _structureServices = structureServices;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //PreLoad faz o backfill da estrutura do dia (via Regenerate) antes que o
            //servidor aceite conexoes, garantindo que o app encontre estrutura desde o
            //primeiro candle mesmo se o servidor iniciar no meio do dia.
            await Task.WhenAll(_structureServices.Select(s => s.PreLoad()));
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
