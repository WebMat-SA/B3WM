using B3WM.Shared.Models.Backtest;

namespace B3WM.Services.Backtest
{
    public interface IStrategyFactory
    {
        IStrategy Create(BacktestConfig config, Func<DateTime, Shared.Models.StructureStorageItem?>? structureProvider = null);
    }

    public sealed class StrategyFactory : IStrategyFactory
    {
        private readonly DataKeeperBase _dataKeeper;
        private readonly ILogger<SmartBreakoutStrategy> _smartLogger;

        public StrategyFactory(DataKeeperBase dataKeeper, ILogger<SmartBreakoutStrategy> smartLogger)
        {
            _dataKeeper = dataKeeper;
            _smartLogger = smartLogger;
        }

        public IStrategy Create(BacktestConfig config, Func<DateTime, Shared.Models.StructureStorageItem?>? structureProvider = null)
        {
            return config.StrategyName switch
            {
                StrategyType.Breakout => new SimpleBreakoutStrategy(config, _dataKeeper, structureProvider),
                StrategyType.SmartBreakout => new SmartBreakoutStrategy(_dataKeeper, config, _smartLogger, structureProvider),
                _ => throw new ArgumentException($"Unknown strategy: {config.StrategyName}")
            };
        }
    }

    /// <summary>Factory para o modo ao vivo (sem DataKeeper; estrutura via provider).</summary>
    public static class LiveStrategyFactory
    {
        public static IStrategy Create(
            BacktestConfig config,
            Func<DateTime, Shared.Models.StructureStorageItem?>? structureProvider,
            ILogger<SmartBreakoutStrategy> smartLogger)
        {
            return config.StrategyName switch
            {
                StrategyType.Breakout => new SimpleBreakoutStrategy(config, null, structureProvider),
                StrategyType.SmartBreakout => new SmartBreakoutStrategy(null, config, smartLogger, structureProvider),
                _ => throw new ArgumentException($"Unknown strategy: {config.StrategyName}")
            };
        }
    }
}
