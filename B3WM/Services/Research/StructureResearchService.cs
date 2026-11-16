using System.Collections.Concurrent;
using B3WM.Services.Core;
using B3WM.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace B3WM.Services.Research
{
    /// <summary>
    /// Experimento da issue #25: roda o mesmo detector de estrutura em três
    /// variantes (manual, ATR por perna e ATR por barra), lado a lado, sem
    /// tocar na estrutura de produção. Persiste em Data/Research.
    /// </summary>
    public sealed class StructureResearchService
    {
        private readonly string _symbol;
        private readonly int _timeFrame;
        private readonly StructureResearchOptions _options;
        private readonly CandleService? _candleService;
        private readonly StructureService? _structureService;
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<StructureResearchService> _logger;
        private readonly double _tickSize;

        private readonly object _gate = new();
        private readonly SemaphoreSlim _processLock = new(1, 1);
        private readonly ConcurrentQueue<BarStorageItem> _pending = new();
        private readonly Dictionary<string, List<StructureResearchPoint>> _points = new();
        private readonly Dictionary<string, StructureEngine> _engines = new();
        private readonly AtrState _atr;

        private double? _legRange;
        private DateTime _lastProcessed = DateTime.MinValue;
        private bool _ready;
        private bool _subscribed;

        public StructureResearchService(
            string symbol,
            int timeFrame,
            StructureResearchOptions options,
            IEnumerable<CandleService> candleServices,
            IEnumerable<StructureService> structureServices,
            IServiceScopeFactory scopes,
            ILogger<StructureResearchService> logger)
        {
            _symbol = symbol;
            _timeFrame = timeFrame;
            _options = options;
            _candleService = candleServices.FirstOrDefault(
                c => c.Symbol == symbol && c.TimeFrame == timeFrame);
            _structureService = structureServices.FirstOrDefault(
                s => s.Symbol == symbol && s.TimeFrame == timeFrame);
            _scopes = scopes;
            _logger = logger;
            _tickSize = Defaults.GetTickSize(symbol);
            _atr = new AtrState(options.AtrPeriod);

            Reset();

            if (_candleService == null)
            {
                _logger.LogWarning(
                    "[structure-research] CandleService ausente para {Symbol} TF{TimeFrame}",
                    symbol, timeFrame);
            }
        }

        public string Symbol => _symbol;
        public int TimeFrame => _timeFrame;
        public bool Enabled => _options.Enabled;

        public void Subscribe()
        {
            if (_subscribed || _candleService == null)
                return;

            _candleService.OnUpdate += OnCandleAsync;
            _subscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_subscribed || _candleService == null)
                return;

            _candleService.OnUpdate -= OnCandleAsync;
            _subscribed = false;
        }

        private Task OnCandleAsync(BarStorageItem bar)
        {
            if (bar.Symbol != _symbol || bar.TimeFrame != _timeFrame)
                return Task.CompletedTask;

            // Durante o preload os eventos ficam na fila; Drain processa em
            // ordem, sem perder o candle que fechou no meio do replay.
            lock (_gate)
            {
                if (!_ready)
                {
                    _pending.Enqueue(bar);
                    return Task.CompletedTask;
                }
            }

            return ProcessLiveAsync(bar);
        }

        /// <summary>
        /// Reprocessa os candles dos últimos <see cref="StructureResearchOptions.WarmupDays"/>
        /// dias para aquecer ATR e estrutura, e persiste os pontos gerados.
        /// </summary>
        public async Task PreLoadAsync(CancellationToken ct = default)
        {
            if (!Enabled)
                return;

            lock (_gate)
            {
                _ready = false;
            }

            Reset();

            var dates = new List<DateTime>();
            var oldest = DateTime.Today.AddDays(-Math.Max(0, _options.WarmupDays));
            for (var date = oldest.Date; date <= DateTime.Today; date = date.AddDays(1))
            {
                ct.ThrowIfCancellationRequested();
                var bars = await ReadCandlesAsync(date, ct);
                if (bars.Count == 0)
                    continue;

                dates.Add(date.Date);
                foreach (var bar in bars.OrderBy(b => b.Date))
                    ProcessBar(bar);
            }

            lock (_gate)
            {
                _lastProcessed = _points.Values
                    .SelectMany(p => p)
                    .Select(p => p.Date)
                    .DefaultIfEmpty(DateTime.MinValue)
                    .Max();
            }

            if (_options.Persist)
            {
                foreach (var date in dates.Distinct())
                    await PersistDateAsync(date, ct);
            }

            _logger.LogInformation(
                "[structure-research] preload {Symbol} TF{TimeFrame}: {Dates} dias, último={Last:O}",
                _symbol, _timeFrame, dates.Count, _lastProcessed);
        }

        /// <summary>
        /// Ativa o modo live e processa os candles que chegaram durante o
        /// preload (evita buraco entre o replay e a subscrição).
        /// </summary>
        public async Task DrainAsync(CancellationToken ct = default)
        {
            if (!Enabled)
                return;

            // Segura o process lock, ativa o live e drena a fila na mesma
            // seção crítica. Eventos novos esperam no lock; nenhum candle
            // enfileirado antes da troca é perdido nem processado fora de ordem.
            await _processLock.WaitAsync(ct);
            try
            {
                List<BarStorageItem> queued;
                lock (_gate)
                {
                    _ready = true;
                    queued = new List<BarStorageItem>();
                    while (_pending.TryDequeue(out var bar))
                        queued.Add(bar);
                }

                foreach (var bar in queued.OrderBy(b => b.Date))
                {
                    if (bar.Date <= _lastProcessed)
                        continue;

                    ProcessBar(bar);
                    _lastProcessed = bar.Date;
                }

                if (_options.Persist && _lastProcessed != DateTime.MinValue)
                    await PersistDateAsync(_lastProcessed.Date, ct);
            }
            finally
            {
                _processLock.Release();
            }
        }

        private async Task ProcessLiveAsync(
            BarStorageItem bar,
            CancellationToken ct = default,
            bool persist = true)
        {
            await _processLock.WaitAsync(ct);
            try
            {
                if (bar.Date <= _lastProcessed)
                    return;

                ProcessBar(bar);
                _lastProcessed = bar.Date;

                if (persist && _options.Persist)
                    await PersistDateAsync(bar.Date.Date, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[structure-research] falha processando {Symbol} TF{TimeFrame} {Date:O}",
                    _symbol, _timeFrame, bar.Date);
            }
            finally
            {
                _processLock.Release();
            }
        }

        private void ProcessBar(BarStorageItem bar)
        {
            var atr = _atr.Add(bar);
            var atrTicks = _tickSize > 0 ? atr / _tickSize : 0;
            var manualRange = CurrentManualRange();
            var perBarRange = EffectiveRange(atrTicks);

            if (_legRange is null || _legRange <= 0)
                _legRange = perBarRange;

            foreach (var variant in StructureResearchVariants.All)
            {
                var range = variant switch
                {
                    StructureResearchVariants.Manual => manualRange,
                    StructureResearchVariants.AtrPerLeg => _legRange.Value,
                    _ => perBarRange,
                };

                var engine = _engines[variant];
                var step = engine.Step(bar, range);
                var state = engine.State!;

                var point = new StructureResearchPoint
                {
                    Date = bar.Date,
                    Symbol = _symbol,
                    TimeFrame = _timeFrame,
                    Variant = variant,
                    Open = bar.Open,
                    High = bar.High,
                    Low = bar.Low,
                    Close = bar.Close,
                    UpBorder = state.UpBorder,
                    DownBorder = state.DownBorder,
                    UpAuxBorder = state.UpAuxBorder,
                    DownAuxBorder = state.DownAuxBorder,
                    RangeUsed = range,
                    Atr = atr,
                    AtrTicks = atrTicks,
                    UpChanged = step.UpChanged,
                    DownChanged = step.DownChanged,
                };

                lock (_gate)
                {
                    _points[variant].Add(point);
                }

                if (step.Changed)
                {
                    _logger.LogInformation(
                        "[structure-research] {Symbol} TF{TimeFrame} {Variant} {Date:yyyy-MM-dd HH:mm} " +
                        "change up={UpChanged} down={DownChanged} upBorder={UpBorder:F1} " +
                        "downBorder={DownBorder:F1} range={Range:F1} atr={Atr:F1}",
                        _symbol, _timeFrame, variant, bar.Date,
                        step.UpChanged, step.DownChanged, state.UpBorder,
                        state.DownBorder, range, atr);
                }

                // Nova perna: congela o range para o próximo candle. O candle
                // atual ainda registra o range que confirmou a mudança.
                if (variant == StructureResearchVariants.AtrPerLeg && step.Changed)
                    _legRange = EffectiveRange(atrTicks);
            }
        }

        private double CurrentManualRange()
        {
            var range = _structureService?._minDistanceUpdateBorder ?? 0;
            return range > 0 ? range : Defaults.GetMinDistance(_symbol);
        }

        private double EffectiveRange(double atrTicks)
        {
            if (atrTicks <= 0 || _tickSize <= 0)
                return CurrentManualRange();

            var range = AtrCalculator.ClampRangePoints(
                atrTicks * _tickSize,
                _options.MultiplierFor(_symbol),
                _tickSize,
                _options.AtrFloorTicks,
                _options.AtrCapTicks);

            return range > 0 ? range : CurrentManualRange();
        }

        private async Task<List<BarStorageItem>> ReadCandlesAsync(
            DateTime date, CancellationToken ct)
        {
            if (_candleService == null)
                return new List<BarStorageItem>();

            var path = $"{_symbol}_CandleService_{_timeFrame}MIN_{date:yyyy-MM-dd}.json";
            try
            {
                var bars = await _candleService.GetDataAsync(path);
                return bars ?? new List<BarStorageItem>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[structure-research] falha lendo candles {Symbol} TF{TimeFrame} {Date:yyyy-MM-dd}",
                    _symbol, _timeFrame, date);
                return new List<BarStorageItem>();
            }
        }

        private async Task PersistDateAsync(DateTime date, CancellationToken ct)
        {
            Dictionary<string, IReadOnlyList<StructureResearchPoint>> snapshot;
            lock (_gate)
            {
                snapshot = StructureResearchVariants.All.ToDictionary(
                    variant => variant,
                    variant => (IReadOnlyList<StructureResearchPoint>)_points[variant]
                        .Where(p => p.Date.Date == date.Date)
                        .OrderBy(p => p.Date)
                        .ToList());
            }

            if (snapshot.Values.All(points => points.Count == 0))
                return;

            using var scope = _scopes.CreateScope();
            var keeper = scope.ServiceProvider.GetRequiredService<DataKeeperBase>();

            foreach (var (variant, points) in snapshot)
            {
                if (points.Count == 0)
                    continue;

                var path =
                    $"Research/Structures/{_symbol}_{_timeFrame}MIN_{variant}_{date:yyyy-MM-dd}.json";
                await keeper.WriteDataAsync(path, points);
            }

            var report = StructureResearchReportBuilder.Build(
                _symbol, _timeFrame, date, snapshot,
                _options.FalseBreakBarsShort,
                _options.FalseBreakBarsLong,
                _options.AtrPeriod,
                _options.MultiplierFor(_symbol),
                CurrentManualRange());

            var reportPath =
                $"Research/Reports/{_symbol}_{_timeFrame}MIN_{date:yyyy-MM-dd}.json";
            await keeper.WriteDataAsync(reportPath, report);
        }

        private void Reset()
        {
            _atr.Reset();
            _legRange = null;
            _lastProcessed = DateTime.MinValue;

            lock (_gate)
            {
                _points.Clear();
                _engines.Clear();
                foreach (var variant in StructureResearchVariants.All)
                {
                    _points[variant] = new List<StructureResearchPoint>();
                    _engines[variant] = new StructureEngine();
                }
            }
        }
    }
}
