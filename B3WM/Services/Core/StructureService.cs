using B3WM.Services;
using B3WM.Services.Research;
using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Globalization;
using System.Threading.Channels;

namespace B3WM.Services.Core
{
    public class StructureService : DataKeeperService<List<StructureStorageItem>>, ISymbolable
    {
        public string Symbol { get; }
        public int TimeFrame { get; private set; }

        private readonly IHubContext<DataHub, IDataHubClient> hubContext;
        private readonly StructureRangeResolver _rangeResolver;
        private readonly StructureResearchOptions _rangeOptions;
        private readonly ILogger<StructureService> _logger;

        public event Func<StructureStorageItem, Task>? OnUpdate;

        public override string Path => GetPathForDate(DateTime.Now);

        /// <summary>
        /// Chave estável usada no nome do arquivo. Manual mantém o range
        /// numérico (compatível com os arquivos existentes); ATR usa a chave
        /// do modo, pois o range varia candle a candle.
        /// </summary>
        public string StorageKey => RangeMode switch
        {
            StructureRangeModes.AtrPerLeg => "ATR_LEG",
            StructureRangeModes.AtrPerBar => "ATR_BAR",
            _ => _minDistanceUpdateBorder.ToString(CultureInfo.CurrentCulture),
        };

        public string GetPathForDate(DateTime date) =>
            $"{Symbol}_{nameof(StructureService)}_{TimeFrame}MIN_{StorageKey}_{date:yyyy-MM-dd}.json";

        //variaveis de memoria
        StructureStorageItem? _lastStructure { get; set; }
        private StructureEngine _engine = new();
        private readonly AtrState _atr;
        private DateTime _lastAtrDate = DateTime.MinValue;
        private double? _legRange;
        private DateTime _preloadedFor;

        /// <summary>Modo vigente do range intraday (manual | atr_leg | atr_bar).</summary>
        public string RangeMode { get; private set; } = StructureRangeModes.Manual;

        public StructureStorageItem? GetLastStructure()
            => _lastStructure?.Clone() as StructureStorageItem;

        #region Calculating
        public double _minDistanceUpdateBorder { get; set; }
        #endregion

        /// <summary>
        /// Semeia a estrutura + engine a partir do último registro persistido.
        /// As flags de expectativa não são persistidas (comportamento antigo).
        /// </summary>
        private void SetLastStructure(StructureStorageItem item)
        {
            _lastStructure = item;
            _engine = new StructureEngine(StructureEngineState.FromStructure(item));
        }


        public StructureService(
            string symbol,
            int timeFrame,
            double minDistance,
            IHubContext<DataHub, IDataHubClient> hubContext,
            IServiceProvider serviceProvider,
            StructureRangeResolver rangeResolver,
            StructureResearchOptions rangeOptions,
            ILogger<StructureService> logger)
            : base(serviceProvider)
        {
            Symbol = symbol;
            TimeFrame = timeFrame;
            _minDistanceUpdateBorder = minDistance;
            this.hubContext = hubContext;
            _rangeResolver = rangeResolver;
            _rangeOptions = rangeOptions;
            _logger = logger;
            _atr = new AtrState(rangeOptions.AtrPeriod);
        }

        public async Task PreLoad()
        {
            var today = DateTime.Today;
            if (_preloadedFor == today)
                return;

            // Carrega o modo intraday persistido antes de montar o Path: em
            // ATR o arquivo usa a chave do modo, não o range numérico.
            await LoadModeAsync();

            // Continua no último range usado (deduzido dos arquivos) em vez de
            // voltar ao default no restart. Precisa acontecer antes de montar o
            // Path, para ler/gravar no mesmo range do front.
            var deducedDistance = await _rangeResolver.ResolveAsync(Symbol, TimeFrame == 1440);
            if (deducedDistance is > 0)
                _minDistanceUpdateBorder = deducedDistance.Value;

            //fazer aqui, carregar dos arquivos do Data, para saber a ultima structure (mesmo sendo do dia anterior)
            var allData = await GetDataAsync();

            DataKeep = allData ?? new List<StructureStorageItem>();

            if (allData is { Count: > 0 })
            { 
                var last = allData.OrderByDescending(q => q.Date).FirstOrDefault();
                if (last != null)
                {
                    SetLastStructure(last);
                }
            }
            else
            {
                //gerar novos paths para dia anteriores (no max 7) para tentar achar a ultima structure, caso nao ache, entao inicia normalmente
                for(int i = 0; i < 7; i++)
                {
                    var date = today.AddDays(-i);
                    var path = GetPathForDate(date);
                    var data = await GetDataAsync(path);
                    if (data is { Count: > 0 })
                    {
                        var last = data.OrderByDescending(q => q.Date).FirstOrDefault();
                        if (last != null)
                        {
                            SetLastStructure(last);
                            break;
                        }
                    }
                }

                //se o _lastStructure ainda for nulo, vamos gerar a partir dos candles dos ultimos 7 dias
                if (_lastStructure == null)
                    await Regenerate();
            }

            // 1440: se o servidor ficou fora no fechamento diário, a estrutura
            // fica parada no último dia persistido. Faz backfill incremental
            // dos candles diários faltantes (sem recalcular 365 dias).
            if (TimeFrame == 1440 && _lastStructure != null)
                await BackfillDailyAsync();

            // Modo ATR retomado de arquivo: reaquece o ATR e o range da perna
            // sem precisar regerar os 7 dias. Se o Regenerate já rodou, o
            // estado veio do replay e este método é no-op.
            await WarmAtrAsync();

            _preloadedFor = today;
        }

        /// <summary>
        /// Processa apenas os candles diários posteriores à última estrutura
        /// conhecida. É o caso de o servidor ter ficado off na virada do dia:
        /// o candle 1440 fecha no primeiro tick do dia seguinte e, sem o
        /// servidor ligado, o StructureService não calcula aquele dia.
        /// Persiste o resultado no arquivo da última data processada.
        /// </summary>
        private async Task BackfillDailyAsync()
        {
            var candleService = _serviceProvider.GetServices<CandleService>()
                .FirstOrDefault(q => q.Symbol == Symbol && q.TimeFrame == TimeFrame);

            if (candleService == null || _lastStructure == null ||
                _lastStructure.Date == default)
                return;

            var today = DateTime.Today;
            var start = _lastStructure.Date.Date.AddDays(1);
            if (start > today)
                return;

            // Mesmo limite do Regenerate diário: não varre anos de arquivos.
            var minStart = today.AddDays(-365);
            if (start < minStart)
                start = minStart;

            DataKeep ??= new List<StructureStorageItem>();

            var processed = 0;
            var lastProcessedDate = _lastStructure.Date.Date;

            for (var date = start; date <= today; date = date.AddDays(1))
            {
                var candlePath =
                    $"{Symbol}_{nameof(CandleService)}_{TimeFrame}MIN_{date:yyyy-MM-dd}.json";

                var candles = await candleService.GetDataAsync(candlePath);
                if (candles is not { Count: > 0 })
                    continue;

                foreach (var candle in candles.OrderBy(c => c.Date))
                {
                    if (candle == null)
                        continue;

                    await Calculate(candle, skipPreLoad: true, broadcast: false, persist: false);
                    processed++;
                    lastProcessedDate = candle.Date.Date;
                }
            }

            if (processed == 0)
                return;

            // Remove duplicatas de data (o arquivo do dia pode já conter parte
            // do histórico) e mantém a versão mais recente de cada dia.
            DataKeep = DataKeep
                .GroupBy(s => s.Date.Date)
                .Select(g => g.OrderByDescending(s => s.Date).First())
                .OrderBy(s => s.Date)
                .ToList();

            var persistPath =
                GetPathForDate(lastProcessedDate);

            await WriteFileAsync(DataKeep, persistPath);

            _logger.LogInformation(
                "[structure-backfill] {Symbol} {TimeFrame}MIN: {Processed} candles, last={Last:yyyy-MM-dd}",
                Symbol, TimeFrame, processed, _lastStructure?.Date);
        }

        public async Task Regenerate(bool broadcastEach = false)
        {
            var candleService = _serviceProvider.GetServices<CandleService>().FirstOrDefault(q=>q.Symbol == Symbol && q.TimeFrame == TimeFrame);

            if (candleService == null)
            {
                throw new Exception($"CandleService for symbol {Symbol} and timeframe {TimeFrame} not found.");
            }

            // Diário (1440) é histórico estático: cobre janela longa para que
            // thresholds custom (ex. WINFUT 3000) gerem histórico real em vez
            // de ~7 pontos. Intraday mantém 7 dias (comportamento atual).
            var days = TimeFrame == 1440 ? 365 : 7;
            var today = DateTime.Today;
            var hasTodayCandles = false;
            DataKeep ??= new List<StructureStorageItem>();

            // Replay completo: o ATR e a perna corrente precisam ser
            // reconstruídos do zero para não misturar com o modo anterior.
            _atr.Reset();
            _lastAtrDate = DateTime.MinValue;
            _legRange = null;

            //buscar todos os candles dos ultimos N dias, gerar a estrutura e salvar
            for (int i = days - 1; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var candlePath = $"{Symbol}_{nameof(CandleService)}_{TimeFrame}MIN_{date:yyyy-MM-dd}.json";
                var candles = await candleService.GetDataAsync(candlePath);
                if (candles is { Count: > 0 })
                {
                    if (date.Date == today)
                        hasTodayCandles = true;

                    foreach (var candle in candles)
                    {
                        if (candle != null)
                            await Calculate(candle, true, broadcastEach, persist: false);
                    }
                }
            }

            // Backfill é cumulativo na memória; grava uma única vez e somente
            // quando existe candle do pregão de hoje (não cria arquivo em
            // fds/feriado/sem ticks).
            if (hasTodayCandles && DataKeep.Count > 0)
            {
                await SetDataAsync(DataKeep);

                // ATR não tem arquivo histórico anterior (o range numérico não
                // identifica o modo); grava também os dias do replay para o
                // multi-day e o automode encontrarem a série.
                if (RangeMode != StructureRangeModes.Manual)
                {
                    foreach (var group in DataKeep.GroupBy(s => s.Date.Date))
                    {
                        if (group.Key == today)
                            continue;

                        await WriteFileAsync(
                            group.OrderBy(s => s.Date).ToList(),
                            GetPathForDate(group.Key));
                    }
                }
            }
        }

        public async Task<StructureStorageItem> Calculate(
            BarStorageItem newBar, bool skipPreLoad = false, bool broadcast = true, bool persist = true)
        {
            try
            {
                var result = await Generate(newBar, skipPreLoad);

                // Backfill silencioso (Regenerate do Confirm/startup) não
                // emite N pushes: o chamador decide o push final único.
                if (broadcast)
                {
                    if (hubContext != null)
                    {
                        await hubContext.Clients.Group(Symbol).ReceiveOnStructure(result);
                    }

                    if (OnUpdate != null) await OnUpdate.Invoke(result);
                }

                DataKeep ??= new List<StructureStorageItem>();
                DataKeep.Add((StructureStorageItem)result.Clone());

                if (persist && DataPersistencePolicy.ShouldPersistStructure(newBar, DateTime.Today))
                    await SetDataAsync(DataKeep);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StructureService.Calculate error");
                return _lastStructure ?? new StructureStorageItem
                {
                    Symbol = newBar.Symbol,
                    Date = newBar.Date,
                    TimeFrame = newBar.TimeFrame,
                    UpBorder = newBar.High,
                    DownBorder = newBar.Low,
                    UpAuxBorder = newBar.High,
                    DownAuxBorder = newBar.Low
                };
            }
        }

        private async Task<StructureStorageItem> Generate(BarStorageItem newBar, bool skipPreLoad = false)
        {
            //se for primeiro candle
            if (!_engine.IsInitialized)
            {
                if (!skipPreLoad) await PreLoad();

                if (!_engine.IsInitialized)
                {
                    FeedAtr(newBar);
                    var initialRange = ResolveRange();
                    _engine.Step(newBar, initialRange);
                    return Store(newBar, initialRange);
                }
            }

            FeedAtr(newBar);
            var range = ResolveRange();
            var step = _engine.Step(newBar, range);

            if (RangeMode == StructureRangeModes.AtrPerLeg && step.Changed)
                _legRange = EffectiveAtrRange();

            return Store(newBar, range);
        }

        private void FeedAtr(BarStorageItem bar)
        {
            if (bar.Date <= _lastAtrDate)
                return;

            _atr.Add(bar);
            _lastAtrDate = bar.Date;
        }

        private double ResolveRange()
        {
            if (RangeMode == StructureRangeModes.Manual || _atr.Value <= 0)
                return _minDistanceUpdateBorder;

            if (RangeMode == StructureRangeModes.AtrPerBar)
                return EffectiveAtrRange();

            if (_legRange is null || _legRange <= 0)
                _legRange = EffectiveAtrRange();

            return _legRange.Value;
        }

        private double EffectiveAtrRange()
        {
            if (_atr.Value <= 0)
                return _minDistanceUpdateBorder;

            var tickSize = Defaults.GetTickSize(Symbol);
            if (tickSize <= 0)
                return _minDistanceUpdateBorder;

            var floorTicks = TimeFrame == 1440
                ? _rangeOptions.AtrDailyFloorTicks
                : _rangeOptions.AtrFloorTicks;
            var capTicks = TimeFrame == 1440
                ? _rangeOptions.AtrDailyCapTicks
                : _rangeOptions.AtrCapTicks;

            var multiplier = TimeFrame == 1440
                ? _rangeOptions.DailyMultiplierFor(Symbol)
                : _rangeOptions.MultiplierFor(Symbol);

            var range = AtrCalculator.ClampRangePoints(
                _atr.Value,
                multiplier,
                tickSize,
                floorTicks,
                capTicks);

            return range > 0 ? range : _minDistanceUpdateBorder;
        }

        private StructureStorageItem Store(BarStorageItem bar, double range)
        {
            _lastStructure = _engine.ToStorage(
                bar.Symbol, bar.Date, bar.TimeFrame);
            _lastStructure.RangeMode = RangeMode;
            _lastStructure.RangeUsed = range;
            _lastStructure.Atr = _atr.Value;
            return _lastStructure;
        }

        public async Task SetMinDistance(double minDistance, bool broadcastFinal = true)
        {
            DataKeep = new();
            _lastStructure = null;
            _engine = new StructureEngine();
            _atr.Reset();
            _lastAtrDate = DateTime.MinValue;
            _legRange = null;
            _minDistanceUpdateBorder = minDistance;

            // Regen silencioso + 1 push final com a última estrutura (evita
            // rajada de N ReceiveOnStructure no Confirm; o day-close intraday
            // continua emitindo via Calculate com broadcast=true).
            await Regenerate(broadcastEach: false);

            if (broadcastFinal && _lastStructure?.Clone() is StructureStorageItem finalItem)
            {
                if (hubContext != null)
                {
                    await hubContext.Clients.Group(Symbol).ReceiveOnStructure(finalItem);
                }
                if (OnUpdate != null) await OnUpdate.Invoke(finalItem);
            }

            await PersistModeConfigAsync();
        }

        /// <summary>
        /// Troca o modo (manual | atr_leg | atr_bar), regenera a série no modo
        /// novo e persiste a configuração do símbolo. Funciona no intraday e
        /// no 1440 (campos separados no arquivo de config).
        /// </summary>
        public async Task SetRangeModeAsync(string mode, bool broadcastFinal = true)
        {
            var normalized = StructureRangeModes.Normalize(mode);
            if (!StructureRangeModes.IsValid(normalized))
                return;

            RangeMode = normalized;
            DataKeep = new();
            _lastStructure = null;
            _engine = new StructureEngine();
            _atr.Reset();
            _lastAtrDate = DateTime.MinValue;
            _legRange = null;

            await Regenerate(broadcastEach: false);

            if (broadcastFinal && _lastStructure?.Clone() is StructureStorageItem finalItem)
            {
                if (hubContext != null)
                {
                    await hubContext.Clients.Group(Symbol).ReceiveOnStructure(finalItem);
                }
                if (OnUpdate != null) await OnUpdate.Invoke(finalItem);
            }

            await PersistModeConfigAsync();
        }

        private async Task LoadModeAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var keeper = scope.ServiceProvider.GetRequiredService<DataKeeperBase>();
                var config = await keeper.ReadDataAsync<StructureModeConfig>(
                    $"{Symbol}_StructureMode.json");

                if (config == null)
                    return;

                if (TimeFrame == 1440)
                {
                    RangeMode = StructureRangeModes.Normalize(config.DailyMode);
                    if (config.DailyManualRange > 0)
                        _minDistanceUpdateBorder = config.DailyManualRange;
                }
                else
                {
                    RangeMode = StructureRangeModes.Normalize(config.Mode);
                    if (config.ManualRange > 0)
                        _minDistanceUpdateBorder = config.ManualRange;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[structure-mode] falha ao carregar modo de {Symbol} {TimeFrame}MIN",
                    Symbol, TimeFrame);
            }
        }

        private async Task<List<BarStorageItem>> ReadRecentDailyBarsAsync(
            CandleService candleService, int needed)
        {
            var result = new List<BarStorageItem>();
            if (needed <= 0)
                return result;

            for (var i = 1; result.Count < needed && i <= 60; i++)
            {
                var date = DateTime.Today.AddDays(-i);
                var path =
                    $"{Symbol}_{nameof(CandleService)}_{TimeFrame}MIN_{date:yyyy-MM-dd}.json";
                var day = await candleService.GetDataAsync(path);
                if (day is not { Count: > 0 })
                    continue;

                var closed = day
                    .Where(b => b.Date.Date < DateTime.Today)
                    .OrderBy(b => b.Date)
                    .ToList();
                if (closed.Count == 0)
                    continue;

                result.InsertRange(0, closed);
            }

            return result.TakeLast(needed).ToList();
        }

        private async Task WarmAtrAsync()
        {
            if (RangeMode == StructureRangeModes.Manual || _atr.Samples > 0)
                return;

            try
            {
                var candleService = _serviceProvider.GetServices<CandleService>()
                    .FirstOrDefault(c => c.Symbol == Symbol && c.TimeFrame == TimeFrame);
                if (candleService == null)
                    return;

                List<BarStorageItem>? bars;
                if (TimeFrame == 1440)
                {
                    // O DataKeep do 1440 só tem o dia corrente (aberto); o ATR
                    // precisa dos últimos dias fechados, lidos dos arquivos.
                    bars = await ReadRecentDailyBarsAsync(
                        candleService, _rangeOptions.AtrPeriod + 1);
                }
                else
                {
                    bars = candleService.DataKeep?
                        .OrderBy(b => b.Date)
                        .ToList();

                    if (bars is not { Count: > 0 })
                    {
                        var path =
                            $"{Symbol}_{nameof(CandleService)}_{TimeFrame}MIN_{DateTime.Today:yyyy-MM-dd}.json";
                        bars = await candleService.GetDataAsync(path);
                    }
                }

                if (bars is not { Count: > 0 })
                    return;

                var take = Math.Min(bars.Count, _rangeOptions.AtrPeriod + 1);
                _atr.Reset();
                _atr.Warmup(bars.TakeLast(take));
                _lastAtrDate = bars[^1].Date;

                _legRange = _lastStructure?.RangeUsed is > 0
                    ? _lastStructure.RangeUsed
                    : EffectiveAtrRange();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[structure-mode] falha ao aquecer ATR de {Symbol} {TimeFrame}MIN",
                    Symbol, TimeFrame);
            }
        }

        private async Task PersistModeConfigAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var keeper = scope.ServiceProvider.GetRequiredService<DataKeeperBase>();
                var path = $"{Symbol}_StructureMode.json";
                var config = await keeper.ReadDataAsync<StructureModeConfig>(path)
                    ?? new StructureModeConfig();

                if (TimeFrame == 1440)
                {
                    config.DailyMode = RangeMode;
                    config.DailyManualRange = _minDistanceUpdateBorder;
                }
                else
                {
                    config.Mode = RangeMode;
                    config.ManualRange = _minDistanceUpdateBorder;
                }

                config.UpdatedAt = DateTime.Now;
                await keeper.WriteDataAsync(path, config);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[structure-mode] falha ao persistir modo de {Symbol} {TimeFrame}MIN",
                    Symbol, TimeFrame);
            }
        }
    }
}
