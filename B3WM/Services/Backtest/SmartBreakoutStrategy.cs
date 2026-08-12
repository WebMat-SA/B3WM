using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using B3WM.Services.Core;

namespace B3WM.Services.Backtest
{
    public class SmartBreakoutStrategy : IStrategy
    {
        private readonly DataKeeperBase? _dataKeeper;
        private readonly BacktestConfig _config;
        private readonly ILogger<SmartBreakoutStrategy>? _logger;
        private readonly Func<DateTime, StructureStorageItem?>? _structureProvider;

        private readonly Dictionary<DateTime, List<BubbleStorageItem>> _bubblesByBar = new();
        private readonly Dictionary<DateTime, StructureStorageItem> _savedStructure = new();
        private readonly object _bubblesLock = new();

        private readonly double _entryThreshold;
        private readonly double _exitThreshold;
        private readonly Dictionary<int, int>? _agentThresholds;
        private readonly int _bubbleThreshold;
        private readonly double _volumePct;
        private readonly double _structureBufferPct;
        private readonly List<int>? _agents;

        private double? _upBorder, _downBorder;
        private double _upAuxBorder, _downAuxBorder;
        private bool _expectBuyDrop = true, _expectSellDrop = true, _isSizeChanger;
        private readonly double _minDistance;

        public List<StructureStorageItem> StructureLines { get; } = new();

        public string Name => "SmartBreakout";

        public SmartBreakoutStrategy(
            DataKeeperBase? dataKeeper,
            BacktestConfig config,
            ILogger<SmartBreakoutStrategy>? logger,
            Func<DateTime, StructureStorageItem?>? structureProvider = null)
        {
            _dataKeeper = dataKeeper;
            _config = config;
            _logger = logger;
            _structureProvider = structureProvider;
            _minDistance = config.MinDistance > 0 ? config.MinDistance : Defaults.GetMinDistance(config.Symbol);
            _entryThreshold = config.SmartEntryThreshold;
            _exitThreshold = config.SmartExitThreshold;
            _agentThresholds = config.AgentThresholds;
            _bubbleThreshold = config.BubbleThreshold;
            _volumePct = config.SmartVolumePct;
            _structureBufferPct = config.SmartStructureBufferPct;
            _agents = config.SmartAgents;
        }

        /// <summary>
        /// Threshold de quantidade do bubble por agente: prioriza o filtro custom
        /// da aba Bubbles (AgentThresholds), depois o threshold padrão do gráfico
        /// (BubbleThreshold), e por fim o fallback de backtest (SmartEntry/Exit).
        /// </summary>
        private double ThresholdFor(int agent, double fallback)
        {
            if (_agentThresholds != null && _agentThresholds.TryGetValue(agent, out var t) && t > 0)
                return t;
            if (_bubbleThreshold > 0)
                return _bubbleThreshold;
            return fallback;
        }

        /// <summary>Alimenta a estratégia com um bubble (usado ao vivo; o backtest pré-carrega via arquivos).</summary>
        public void AddBubble(BubbleStorageItem b)
        {
            if (b.ActionType != Ticks2.ActionType.Buy && b.ActionType != Ticks2.ActionType.Sale)
                return;

            var key = b.Date.GetCandleStart(_config.TimeFrame);
            lock (_bubblesLock)
            {
                if (!_bubblesByBar.TryGetValue(key, out var list))
                {
                    list = new List<BubbleStorageItem>();
                    _bubblesByBar[key] = list;
                }
                list.Add(b);
            }
        }

        public async Task InitializeAsync()
        {
            // modo ao vivo: bubbles/estrutura chegam via eventos
            if (_dataKeeper == null) return;

            var current = _config.StartDate.Date;
            while (current <= _config.EndDate.Date)
            {
                var bubblePath = $"{_config.Symbol}_{nameof(BubbleService)}_{current:yyyy-MM-dd}.json";
                try
                {
                    var bubbles = await _dataKeeper.ReadDataAsync<List<BubbleStorageItem>>(bubblePath);
                    if (bubbles != null)
                    {
                        foreach (var b in bubbles) AddBubble(b);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load bubbles for {Date}", current.ToString("yyyy-MM-dd"));
                }

                // estrutura salva: mesmas bordas que o drawer mostrou ao vivo
                var structurePath = $"{_config.Symbol}_{nameof(StructureService)}_{_config.TimeFrame}MIN_{_minDistance}_{current:yyyy-MM-dd}.json";
                try
                {
                    var structure = await _dataKeeper.ReadDataAsync<List<StructureStorageItem>>(structurePath);
                    if (structure != null)
                    {
                        foreach (var s in structure)
                        {
                            if (!_savedStructure.ContainsKey(s.Date))
                                _savedStructure[s.Date] = s;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load structure for {Date}", current.ToString("yyyy-MM-dd"));
                }

                current = current.AddDays(1);
            }
        }

        public Signal? Evaluate(BarStorageItem bar, bool hasPosition)
        {
            var structure = ResolveStructure(bar);
            if (structure == null) return null;

            List<BubbleStorageItem>? barBubbles;
            lock (_bubblesLock)
            {
                barBubbles = _bubblesByBar.TryGetValue(bar.Date, out var list)
                    ? new List<BubbleStorageItem>(list)
                    : null;
            }

            if (hasPosition)
            {
                if (barBubbles != null && barBubbles.Any(b => (double)b.Amount >= ThresholdFor(b.Agent, _exitThreshold)))
                    return new Signal { Side = OrderSide.Buy, Reason = "Exit: large bubble" };
                return null;
            }

            if (barBubbles == null) return null;

            var range = structure.UpBorder - structure.DownBorder;
            if (range <= 0) return null;

            var buffer = range * _structureBufferPct;
            var largeBubbles = barBubbles
                .Where(b => (double)b.Amount >= ThresholdFor(b.Agent, _entryThreshold))
                .Where(b => _agents == null || _agents.Contains(b.Agent))
                .ToList();

            foreach (var bubble in largeBubbles)
            {
                if (!IsLowVolumeLevel(bar, bubble.Price))
                    continue;

                if (Math.Abs(bubble.Price - structure.UpBorder) <= buffer ||
                    Math.Abs(bubble.Price - structure.DownBorder) <= buffer)
                    continue;

                if (bubble.ActionType == Ticks2.ActionType.Buy && bubble.Price < structure.UpBorder)
                {
                    var slPrice = structure.DownBorder - buffer;
                    var tpPrice = structure.UpBorder + buffer;
                    return new Signal { Side = OrderSide.Buy, StopLossPrice = slPrice, TakeProfitPrice = tpPrice, Reason = $"SmartB.Compra {bubble.Amount}@{(Ticks2.Agents)bubble.Agent}" };
                }

                if (bubble.ActionType == Ticks2.ActionType.Sale && bubble.Price > structure.DownBorder)
                {
                    var slPrice = structure.UpBorder + buffer;
                    var tpPrice = structure.DownBorder - buffer;
                    return new Signal { Side = OrderSide.Sell, StopLossPrice = slPrice, TakeProfitPrice = tpPrice, Reason = $"SmartB.Venda {bubble.Amount}@{(Ticks2.Agents)bubble.Agent}" };
                }
            }

            return null;
        }

        private StructureStorageItem? ResolveStructure(BarStorageItem bar)
        {
            // fonte injetada (ao vivo: bordas do StructureService)
            if (_structureProvider != null)
            {
                var p = _structureProvider(bar.Date);
                if (p != null)
                {
                    SyncBorders(p);
                    return p;
                }
            }

            // arquivos salvos (backtest: fiel ao drawer)
            if (_savedStructure.TryGetValue(bar.Date, out var saved))
            {
                SyncBorders(saved);
                return saved;
            }

            // fallback: recompute com a mesma lógica do StructureService
            return UpdateStructure(bar);
        }

        private void SyncBorders(StructureStorageItem s)
        {
            _upBorder = s.UpBorder;
            _downBorder = s.DownBorder;
            _upAuxBorder = s.UpAuxBorder;
            _downAuxBorder = s.DownAuxBorder;
            StructureLines.Add(s.Clone() as StructureStorageItem);
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

            var item = new StructureStorageItem
            {
                Date = bar.Date,
                Symbol = _config.Symbol,
                TimeFrame = _config.TimeFrame,
                UpBorder = _upBorder ?? double.NaN,
                DownBorder = _downBorder ?? double.NaN,
                UpAuxBorder = _upAuxBorder,
                DownAuxBorder = _downAuxBorder
            };

            StructureLines.Add(item.Clone() as StructureStorageItem);
            return item;
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

        public void Reset()
        {
            lock (_bubblesLock)
            {
                _bubblesByBar.Clear();
            }
            _savedStructure.Clear();
            _upBorder = _downBorder = null;
            _upAuxBorder = _downAuxBorder = 0;
            _expectBuyDrop = _expectSellDrop = true;
            _isSizeChanger = false;
            StructureLines.Clear();
        }
    }
}
