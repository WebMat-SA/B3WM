using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;

namespace B3WM.Services.Backtest
{
    public class SimpleBreakoutStrategy : StrategyBase
    {
        private const double StopBufferPct = 0.10;

        private readonly DataKeeperBase? _dataKeeper;

        public override string Name => "Breakout";
        public override string Description => "Rompimento de estrutura com buffer de 10% do range";

        public SimpleBreakoutStrategy(
            BacktestConfig config,
            DataKeeperBase? dataKeeper = null,
            Func<DateTime, StructureStorageItem?>? structureProvider = null)
            : base(config, structureProvider)
        {
            _dataKeeper = dataKeeper;
        }

        public override Task InitializeAsync(CancellationToken ct = default)
            => LoadSavedStructureAsync(_dataKeeper, ct);

        public override EntrySignal? TryGetEntry(BarStorageItem bar)
        {
            var structure = ResolveStructure(bar);
            if (structure == null) return null;

            var range = structure.UpBorder - structure.DownBorder;
            if (range <= 0) return null;

            var stopBuffer = range * StopBufferPct;

            if (bar.Close > structure.UpBorder)
                return new EntrySignal
                {
                    Side = OrderSide.Buy,
                    StopLossPrice = structure.DownBorder - stopBuffer,
                    TakeProfitPrice = structure.UpBorder + stopBuffer,
                    Reason = "Fechou acima da borda superior"
                };

            if (bar.Close < structure.DownBorder)
                return new EntrySignal
                {
                    Side = OrderSide.Sell,
                    StopLossPrice = structure.UpBorder + stopBuffer,
                    TakeProfitPrice = structure.DownBorder - stopBuffer,
                    Reason = "Fechou abaixo da borda inferior"
                };

            return null;
        }

        public override ExitSignal? TryGetExit(BarStorageItem bar, BacktestPosition position) => null;
    }
}
