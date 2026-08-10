using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services
{
    public class VerifierManager
    {
        private readonly IHubContext<DataHub, IDataHubClient> _hub;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly object _lock = new();
        private readonly Dictionary<(string symbol, int timeFrame), SignalVerifierService> _verifiers = new();

        public VerifierManager(
            IHubContext<DataHub, IDataHubClient> hub,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory)
        {
            _hub = hub;
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
        }

        public void Start(string symbol, int timeFrame, VerifierConfig config)
        {
            lock (_lock)
            {
                var key = (symbol, timeFrame);
                if (_verifiers.TryGetValue(key, out var existing))
                {
                    existing.Stop();
                    _verifiers.Remove(key);
                }

                var verifier = new SignalVerifierService(
                    symbol,
                    timeFrame,
                    config,
                    _hub,
                    _serviceProvider,
                    _loggerFactory.CreateLogger<SignalVerifierService>());

                _verifiers[key] = verifier;
                verifier.Start();
            }
        }

        public void Stop(string symbol, int timeFrame)
        {
            lock (_lock)
            {
                var key = (symbol, timeFrame);
                if (_verifiers.TryGetValue(key, out var verifier))
                {
                    verifier.Stop();
                    _verifiers.Remove(key);
                }
            }
        }

        public void Reset(string symbol, int timeFrame)
        {
            lock (_lock)
            {
                if (_verifiers.TryGetValue((symbol, timeFrame), out var verifier))
                    verifier.Reset();
            }
        }

        public VerifierState GetState(string symbol, int timeFrame)
        {
            lock (_lock)
            {
                if (_verifiers.TryGetValue((symbol, timeFrame), out var verifier))
                    return verifier.GetState();
            }

            return new VerifierState
            {
                Symbol = symbol,
                TimeFrame = timeFrame,
                IsRunning = false
            };
        }

        public async Task<List<VerifierLogDay>> Export(string symbol, int timeFrame, DateTime? from, DateTime? to)
        {
            var result = new List<VerifierLogDay>();
            var start = (from ?? DateTime.Today.AddDays(-7)).Date;
            var end = (to ?? DateTime.Today).Date;

            using var scope = _serviceProvider.CreateScope();
            var dataKeeper = scope.ServiceProvider.GetRequiredService<DataKeeperBase>();

            var current = start;
            while (current <= end)
            {
                var path = $"{symbol}_Verifier_{timeFrame}MIN_{current:yyyy-MM-dd}.json";
                try
                {
                    var day = await dataKeeper.ReadDataAsync<VerifierLogDay>(path);
                    if (day != null && day.Symbol == symbol && day.Signals.Count > 0)
                        result.Add(day);
                }
                catch
                {
                    // arquivo inexistente/corrompido no período é ignorado
                }
                current = current.AddDays(1);
            }

            return result;
        }
    }
}
