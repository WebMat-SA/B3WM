using Microsoft.Extensions.Hosting;

namespace B3WM.Services.Research
{
    /// <summary>
    /// Sobe o experimento depois do preload das estruturas de produção:
    /// assina os candles, faz o replay de aquecimento e ativa o modo live.
    /// </summary>
    public sealed class StructureResearchHost : IHostedService
    {
        private readonly StructureResearchOptions _options;
        private readonly IReadOnlyList<StructureResearchService> _services;
        private readonly ILogger<StructureResearchHost> _logger;

        public StructureResearchHost(
            StructureResearchOptions options,
            IEnumerable<StructureResearchService> services,
            ILogger<StructureResearchHost> logger)
        {
            _options = options;
            _services = services.ToList();
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("[structure-research] desligado por configuração");
                return;
            }

            // Assina antes do replay: candles que fecharem no meio do preload
            // ficam na fila e são drenados depois, sem buraco na série.
            foreach (var service in _services)
                service.Subscribe();

            foreach (var service in _services)
            {
                try
                {
                    await service.PreLoadAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[structure-research] preload falhou {Symbol} TF{TimeFrame}",
                        service.Symbol, service.TimeFrame);
                }
            }

            foreach (var service in _services)
            {
                try
                {
                    await service.DrainAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[structure-research] drain falhou {Symbol} TF{TimeFrame}",
                        service.Symbol, service.TimeFrame);
                }
            }

            _logger.LogInformation(
                "[structure-research] ativo: {Count} séries ({Symbols})",
                _services.Count,
                string.Join(",", _services.Select(s => $"{s.Symbol}:{s.TimeFrame}")));
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var service in _services)
                service.Unsubscribe();

            return Task.CompletedTask;
        }
    }
}
