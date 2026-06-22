using B3WM.Shared.Models;

namespace B3WM.Services.Backtest
{
    public class SimpleBreakoutStrategy : IStrategy
    {
        private readonly int _lookback;
        private readonly List<BarStorageItem> _bars = new();

        public string Name => "Breakout";

        public SimpleBreakoutStrategy(int lookback) => _lookback = lookback;

        public Task InitializeAsync() => Task.CompletedTask;

        public Signal? Evaluate(BarStorageItem bar, bool hasPosition)
        {
            _bars.Add(bar);

            if (_bars.Count <= _lookback)
                return null;

            var high = _bars.TakeLast(_lookback).Max(b => b.High);
            var low = _bars.TakeLast(_lookback).Min(b => b.Low);

            if (hasPosition)
            {
                if (bar.Close > high)
                    return new Signal { Side = Shared.Models.Backtest.OrderSide.Buy, Reason = "Reversal up" };
                if (bar.Close < low)
                    return new Signal { Side = Shared.Models.Backtest.OrderSide.Sell, Reason = "Reversal down" };
                return null;
            }

            if (bar.Close > high)
                return new Signal { Side = Shared.Models.Backtest.OrderSide.Buy, Reason = "Breakout high" };
            if (bar.Close < low)
                return new Signal { Side = Shared.Models.Backtest.OrderSide.Sell, Reason = "Breakout low" };

            return null;
        }

        public void Reset() => _bars.Clear();
    }
}
