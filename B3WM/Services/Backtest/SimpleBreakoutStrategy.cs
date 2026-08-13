using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using B3WM.Services.Core;

namespace B3WM.Services.Backtest
{
    public class SimpleBreakoutStrategy : IStrategy
    {
        private const double StopBufferPct = 0.10;

        private readonly DataKeeperBase? _dataKeeper;
        private readonly BacktestConfig _config;
        private readonly Func<DateTime, StructureStorageItem?>? _structureProvider;
        private readonly double _minDistance;

        private readonly Dictionary<DateTime, StructureStorageItem> _savedStructure = new();

        private double? _upBorder, _downBorder;
        private double _upAuxBorder, _downAuxBorder;
        private bool _expectBuyDrop = true, _expectSellDrop = true, _isSizeChanger;

        public string Name => "Breakout";

        public SimpleBreakoutStrategy(
            BacktestConfig config,
            DataKeeperBase? dataKeeper = null,
            Func<DateTime, StructureStorageItem?>? structureProvider = null)
        {
            _config = config;
            _dataKeeper = dataKeeper;
            _structureProvider = structureProvider;
            _minDistance = config.MinDistance > 0 ? config.MinDistance : Defaults.GetMinDistance(config.Symbol);
        }

        public async Task InitializeAsync()
        {
            if (_dataKeeper == null) return;

            var current = _config.StartDate.Date;
            while (current <= _config.EndDate.Date)
            {
                var path = $"{_config.Symbol}_{nameof(StructureService)}_{_config.TimeFrame}MIN_{_minDistance}_{current:yyyy-MM-dd}.json";
                try
                {
                    var structure = await _dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);
                    if (structure != null)
                    {
                        foreach (var s in structure)
                        {
                            if (!_savedStructure.ContainsKey(s.Date))
                                _savedStructure[s.Date] = s;
                        }
                    }
                }
                catch (Exception)
                {
                    // dia sem estrutura salva
                }

                current = current.AddDays(1);
            }
        }

        public Signal? Evaluate(BarStorageItem bar, bool hasPosition)
        {
            var structure = ResolveStructure(bar);
            if (structure == null) return null;

            if (hasPosition) return null;

            var range = structure.UpBorder - structure.DownBorder;
            if (range <= 0) return null;

            var stopBuffer = range * StopBufferPct;

            if (bar.Close > structure.UpBorder)
                return new Signal
                {
                    Side = OrderSide.Buy,
                    StopLossPrice = structure.DownBorder - stopBuffer,
                    TakeProfitPrice = structure.UpBorder + stopBuffer,
                    Reason = "Fechou acima da borda superior"
                };

            if (bar.Close < structure.DownBorder)
                return new Signal
                {
                    Side = OrderSide.Sell,
                    StopLossPrice = structure.UpBorder + stopBuffer,
                    TakeProfitPrice = structure.DownBorder - stopBuffer,
                    Reason = "Fechou abaixo da borda inferior"
                };

            return null;
        }

        private StructureStorageItem? ResolveStructure(BarStorageItem bar)
        {
            if (_structureProvider != null)
            {
                var p = _structureProvider(bar.Date);
                if (p != null)
                {
                    SyncBorders(p);
                    return p;
                }
            }

            if (_savedStructure.TryGetValue(bar.Date, out var saved))
            {
                SyncBorders(saved);
                return saved;
            }

            return UpdateStructure(bar);
        }

        private void SyncBorders(StructureStorageItem s)
        {
            _upBorder = s.UpBorder;
            _downBorder = s.DownBorder;
            _upAuxBorder = s.UpAuxBorder;
            _downAuxBorder = s.DownAuxBorder;
        }

        private StructureStorageItem? UpdateStructure(BarStorageItem bar)
        {
            if (_upBorder == null)
            {
                _upBorder = bar.High;
                _downBorder = bar.Low;
                _upAuxBorder = bar.High;
                _downAuxBorder = bar.Low;
            }
            else
            {
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

            return new StructureStorageItem
            {
                Date = bar.Date,
                Symbol = _config.Symbol,
                TimeFrame = _config.TimeFrame,
                UpBorder = _upBorder ?? double.NaN,
                DownBorder = _downBorder ?? double.NaN,
                UpAuxBorder = _upAuxBorder,
                DownAuxBorder = _downAuxBorder
            };
        }

        public void Reset()
        {
            _savedStructure.Clear();
            _upBorder = _downBorder = null;
            _upAuxBorder = _downAuxBorder = 0;
            _expectBuyDrop = _expectSellDrop = true;
            _isSizeChanger = false;
        }
    }
}