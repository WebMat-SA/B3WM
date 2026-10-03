using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Backtest;
using B3WM.Services.Core;

namespace B3WM.Services.Backtest
{
    public class SmartBreakoutStrategy : StrategyBase, IBubbleConsumer
    {
        private readonly DataKeeperBase? _dataKeeper;
        private readonly ILogger<SmartBreakoutStrategy>? _logger;

        private readonly Dictionary<DateTime, List<BubbleStorageItem>> _bubblesByBar = new();
        private readonly object _bubblesLock = new();

        private readonly double _entryThreshold;
        private readonly double _exitThreshold;
        private readonly Dictionary<int, int>? _agentThresholds;
        private readonly int _bubbleThreshold;
        private readonly double _volumePct;
        private readonly double _structureBufferPct;
        private readonly List<int>? _agents;

        public override string Name => "SmartBreakout";
        public override string Description => "Rompimento com confirmação de bubbles em região de baixo volume";

        public SmartBreakoutStrategy(
            DataKeeperBase? dataKeeper,
            BacktestConfig config,
            ILogger<SmartBreakoutStrategy>? logger,
            Func<DateTime, StructureStorageItem?>? structureProvider = null)
            : base(config, structureProvider)
        {
            _dataKeeper = dataKeeper;
            _logger = logger;
            _entryThreshold = config.SmartEntryThreshold;
            _exitThreshold = config.SmartExitThreshold;
            _agentThresholds = config.AgentThresholds;
            _bubbleThreshold = config.BubbleThreshold;
            _volumePct = config.SmartVolumePct;
            _structureBufferPct = config.SmartStructureBufferPct;
            _agents = config.SmartAgents;
        }

        private double ThresholdFor(int agent, double fallback)
        {
            if (_agentThresholds != null && _agentThresholds.TryGetValue(agent, out var t) && t > 0)
                return t;
            if (_bubbleThreshold > 0)
                return _bubbleThreshold;
            return fallback;
        }

        /// <summary>Alimenta a estratégia com um bubble (usado ao vivo; o backtest pré-carrega via arquivos).</summary>
        public void AddBubble(BubbleStorageItem b) => OnBubble(b);

        public void OnBubble(BubbleStorageItem b)
        {
            if (b.ActionType != Ticks2.ActionType.Buy && b.ActionType != Ticks2.ActionType.Sale)
                return;

            var key = b.Date.GetCandleStart(Config.TimeFrame);
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

        public override async Task InitializeAsync(CancellationToken ct = default)
        {
            // modo ao vivo: bubbles/estrutura chegam via eventos
            if (_dataKeeper == null) return;
            if (Initialized) return;

            var current = Config.StartDate.Date;
            while (current <= Config.EndDate.Date)
            {
                ct.ThrowIfCancellationRequested();
                var bubblePath = $"{Config.Symbol}_{nameof(BubbleService)}_{current:yyyy-MM-dd}.json";
                try
                {
                    var bubbles = await _dataKeeper.ReadDataAsync<List<BubbleStorageItem>>(bubblePath);
                    if (bubbles != null)
                    {
                        foreach (var b in bubbles) OnBubble(b);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load bubbles for {Date}", current.ToString("yyyy-MM-dd"));
                }

                current = current.AddDays(1);
            }

            await LoadSavedStructureAsync(_dataKeeper, ct);
        }

        public override ExitSignal? TryGetExit(BarStorageItem bar, BacktestPosition position)
        {
            // Garante bordas atualizadas mesmo quando só há saída no candle.
            ResolveStructure(bar);

            var barBubbles = GetBarBubbles(bar.Date);
            if (barBubbles != null && barBubbles.Any(b => (double)b.Amount >= ThresholdFor(b.Agent, _exitThreshold)))
                return new ExitSignal { Reason = "Exit: large bubble" };
            return null;
        }

        public override EntrySignal? TryGetEntry(BarStorageItem bar)
        {
            var structure = ResolveStructure(bar);
            if (structure == null) return null;

            var barBubbles = GetBarBubbles(bar.Date);
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
                    return new EntrySignal { Side = OrderSide.Buy, StopLossPrice = slPrice, TakeProfitPrice = tpPrice, Reason = $"SmartB.Compra {bubble.Amount}@{(Ticks2.Agents)bubble.Agent}" };
                }

                if (bubble.ActionType == Ticks2.ActionType.Sale && bubble.Price > structure.DownBorder)
                {
                    var slPrice = structure.UpBorder + buffer;
                    var tpPrice = structure.DownBorder - buffer;
                    return new EntrySignal { Side = OrderSide.Sell, StopLossPrice = slPrice, TakeProfitPrice = tpPrice, Reason = $"SmartB.Venda {bubble.Amount}@{(Ticks2.Agents)bubble.Agent}" };
                }
            }

            return null;
        }

        private List<BubbleStorageItem>? GetBarBubbles(DateTime barDate)
        {
            lock (_bubblesLock)
            {
                return _bubblesByBar.TryGetValue(barDate, out var list)
                    ? new List<BubbleStorageItem>(list)
                    : null;
            }
        }

        private bool IsLowVolumeLevel(BarStorageItem bar, double price)
        {
            if (bar.VolumeLevel == null || bar.VolumeLevel.Count == 0)
                return false;

            var tickSize = Defaults.GetTickSize(Config.Symbol);
            var avg = bar.VolumeLevel.Average(v => (double)v.Total);
            if (avg <= 0) return false;

            var level = bar.VolumeLevel
                .Where(v => Math.Abs(v.Price - price) <= tickSize)
                .OrderBy(v => Math.Abs(v.Price - price))
                .FirstOrDefault();

            return level != null && level.Total < avg * _volumePct;
        }

        public override void Reset()
        {
            base.Reset();
            // Preserva _bubblesByBar e SavedStructure (dados carregados):
            // permite re-run após Reset sem recarregar disco.
        }
    }
}
