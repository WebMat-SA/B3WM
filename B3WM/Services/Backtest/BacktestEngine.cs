using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using B3WM.Services.Core;

namespace B3WM.Services.Backtest
{
    public class BacktestEngine
    {
        private readonly DataKeeperBase _dataKeeper;
        private readonly ILogger<BacktestEngine> _logger;

        public BacktestEngine(DataKeeperBase dataKeeper, ILogger<BacktestEngine> logger)
        {
            _dataKeeper = dataKeeper;
            _logger = logger;
        }

        public async Task<BacktestResult> Run(BacktestConfig config, IStrategy strategy)
        {
            await strategy.InitializeAsync();

            var bars = await LoadBars(config);
            var result = new BacktestResult
            {
                StrategyName = strategy.Name,
                Config = config
            };

            if (bars.Count == 0) return result;

            var simulator = new BacktestSimulator(config, strategy);

            foreach (var bar in bars)
            {
                simulator.ProcessBar(bar);
            }

            var lastBar = bars[^1];
            simulator.ForceClose(ExitReason.EndOfData, lastBar.Close, lastBar.Date);

            simulator.CalculateMetrics(result);

            if (strategy is SmartBreakoutStrategy smart)
                result.StructureLines = smart.StructureLines;

            return result;
        }

        private async Task<List<BarStorageItem>> LoadBars(BacktestConfig config)
        {
            var allBars = new List<BarStorageItem>();
            var current = config.StartDate.Date;

            while (current <= config.EndDate.Date)
            {
                var path = $"{config.Symbol}_{nameof(CandleService)}_{config.TimeFrame}MIN_{current:yyyy-MM-dd}.json";
                try
                {
                    var dayBars = await _dataKeeper.ReadDataAsync<List<BarStorageItem>>(path);
                    allBars.AddRange(dayBars);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load bars for {Date}", current.ToString("yyyy-MM-dd"));
                }
                current = current.AddDays(1);
            }

            return allBars
                .Where(b => b.Date >= config.StartDate && b.Date <= config.EndDate)
                .OrderBy(b => b.Date)
                .ToList();
        }
    }
}
