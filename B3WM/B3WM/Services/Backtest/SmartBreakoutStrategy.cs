using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using B3WM.Services.Core;

namespace B3WM.Services.Backtest
{
    public class SmartBreakoutStrategy : IStrategy
    {
        private readonly DataKeeperBase _dataKeeper;
        private readonly BacktestConfig _config;
        private readonly Dictionary<DateTime, List<BubbleStorageItem>> _bubblesByBar = new();
        private readonly int _thresholdEntry = Defaults.Backtest.SmartEntryThreshold;
        private readonly int _thresholdExit = Defaults.Backtest.SmartExitThreshold;
        private readonly double _volumePct = Defaults.Backtest.SmartVolumePct;
        private readonly double _structureBufferPct = Defaults.Backtest.SmartStructureBufferPct;

        private double? _upBorder, _downBorder;
        private double _upAuxBorder, _downAuxBorder;
        private bool _expectBuyDrop = true, _expectSellDrop = true, _isSizeChanger;
        private readonly double _minDistance;

        public string Name => "SmartBreakout";

        public SmartBreakoutStrategy(DataKeeperBase dataKeeper, BacktestConfig config)
        {
            _dataKeeper = dataKeeper;
            _config = config;
            _minDistance = Defaults.GetMinDistance(config.Symbol);
        }

        public async Task InitializeAsync()
        {
            var current = _config.StartDate.Date;
            while (current <= _config.EndDate.Date)
            {
                var path = $"{_config.Symbol}_{nameof(BubbleService)}_{current:yyyy-MM-dd}.json";
                try
                {
                    var bubbles = await _dataKeeper.ReadDataAsync<List<BubbleStorageItem>>(path);
                    if (bubbles != null)
                    {
                        foreach (var b in bubbles)
                        {
                            if (b.ActionType != Ticks2.ActionType.Buy && b.ActionType != Ticks2.ActionType.Sale)
                                continue;
                            var key = b.Date.GetCandleStart(_config.TimeFrame);
                            if (!_bubblesByBar.ContainsKey(key))
                                _bubblesByBar[key] = new List<BubbleStorageItem>();
                            _bubblesByBar[key].Add(b);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load bubbles for {current:yyyy-MM-dd}: {ex.Message}");
                }
                current = current.AddDays(1);
            }
        }

        public Signal? Evaluate(BarStorageItem bar, bool hasPosition)
        {
            UpdateStructure(bar);
            var hasBubbles = _bubblesByBar.TryGetValue(bar.Date, out var barBubbles);

            if (hasPosition)
            {
                if (hasBubbles && barBubbles!.Any(b => b.Amount >= _thresholdExit))
                    return new Signal { Side = OrderSide.Buy, Reason = "Exit: large bubble" };
                return null;
            }

            if (_upBorder == null || _downBorder == null || !hasBubbles)
                return null;

            var range = _upBorder.Value - _downBorder.Value;
            if (range <= 0) return null;

            var buffer = range * _structureBufferPct;
            var largeBubbles = barBubbles!.Where(b => b.Amount >= _thresholdEntry).ToList();

            foreach (var bubble in largeBubbles)
            {
                if (!IsLowVolumeLevel(bar, bubble.Price))
                    continue;

                if (Math.Abs(bubble.Price - _upBorder.Value) <= buffer ||
                    Math.Abs(bubble.Price - _downBorder.Value) <= buffer)
                    continue;

                if (bubble.ActionType == Ticks2.ActionType.Buy && bubble.Price < _upBorder.Value)
                {
                    var slPrice = _downBorder.Value - buffer;
                    return new Signal { Side = OrderSide.Buy, StopLossPrice = slPrice, Reason = $"SmartB.Compra {bubble.Amount}@{(Ticks2.Agents)bubble.Agent}" };
                }

                if (bubble.ActionType == Ticks2.ActionType.Sale && bubble.Price > _downBorder.Value)
                {
                    var slPrice = _upBorder.Value + buffer;
                    return new Signal { Side = OrderSide.Sell, StopLossPrice = slPrice, Reason = $"SmartB.Venda {bubble.Amount}@{(Ticks2.Agents)bubble.Agent}" };
                }
            }

            return null;
        }

        private bool IsLowVolumeLevel(BarStorageItem bar, double price)
        {
            if (bar.VolumeLevel == null || bar.VolumeLevel.Count == 0)
                return false;

            var tickSize = Defaults.GetTickSize(_config.Symbol);
            var avg = bar.VolumeLevel.Average(v => (double)v.Total);
            if (avg <= 0) return false;

            var level = bar.VolumeLevel
                .Where(v => Math.Abs(v.Price - price) <= tickSize)
                .OrderBy(v => Math.Abs(v.Price - price))
                .FirstOrDefault();

            return level != null && level.Total < avg * _volumePct;
        }

        private void UpdateStructure(BarStorageItem bar)
        {
            if (_upBorder == null)
            {
                _upBorder = bar.High;
                _downBorder = bar.Low;
                _upAuxBorder = bar.High;
                _downAuxBorder = bar.Low;
                return;
            }

            var virtualUpAux = Math.Max(_upAuxBorder, bar.High);
            var virtualDownAux = Math.Min(_downAuxBorder, bar.Low);

            if (virtualUpAux - bar.Close >= _minDistance && _expectBuyDrop)
            {
                _upAuxBorder = virtualUpAux;
                _upBorder = _upAuxBorder;
                _expectSellDrop = true;
                _expectBuyDrop = false;
                _downAuxBorder = bar.Low;
                _isSizeChanger = true;
            }

            if (bar.Close - virtualDownAux >= _minDistance && _expectSellDrop && !_isSizeChanger)
            {
                _downAuxBorder = virtualDownAux;
                _downBorder = _downAuxBorder;
                _expectBuyDrop = true;
                _expectSellDrop = false;
                _upAuxBorder = bar.High;
            }

            _upAuxBorder = Math.Max(_upAuxBorder, bar.High);
            _downAuxBorder = Math.Min(_downAuxBorder, bar.Low);
            _isSizeChanger = false;
        }

        public void Reset()
        {
            _bubblesByBar.Clear();
            _upBorder = _downBorder = null;
            _upAuxBorder = _downAuxBorder = 0;
            _expectBuyDrop = _expectSellDrop = true;
            _isSizeChanger = false;
        }
    }
}
