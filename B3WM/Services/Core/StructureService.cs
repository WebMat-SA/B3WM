using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace B3WM.Services.Core
{
    public class StructureService : DataKeeperService<List<StructureStorageItem>>, ISymbolable
    {
        public string Symbol { get; }
        public int TimeFrame { get; private set; }

        private readonly IHubContext<DataHub, IDataHubClient> hubContext;
        private readonly StructureRangeResolver _rangeResolver;
        private readonly ILogger<StructureService> _logger;

        public event Func<StructureStorageItem, Task>? OnUpdate;

        public override string Path => $"{Symbol}_{nameof(StructureService)}_{TimeFrame}MIN_{_minDistanceUpdateBorder}_{DateTime.Now:yyyy-MM-dd}.json";

        //variaveis de memoria
        StructureStorageItem? _lastStructure { get; set; }
        private DateTime _preloadedFor;

        public StructureStorageItem? GetLastStructure()
            => _lastStructure?.Clone() as StructureStorageItem;

        #region Calculating
        public double _minDistanceUpdateBorder { get; set; }
        bool expectBuyDrop = true;
        bool expectSellDrop = true;
        bool isSizeChanger = false;
        #endregion


        public StructureService(string symbol, int timeFrame, double minDistance ,IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider, StructureRangeResolver rangeResolver, ILogger<StructureService> logger)
            : base(serviceProvider)
        {
            Symbol = symbol;
            TimeFrame = timeFrame;
            _minDistanceUpdateBorder = minDistance;
            this.hubContext = hubContext;
            _rangeResolver = rangeResolver;
            _logger = logger;
        }

        public async Task PreLoad()
        {
            var today = DateTime.Today;
            if (_preloadedFor == today)
                return;

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
                    _lastStructure = last;
                }
            }
            else
            {
                //gerar novos paths para dia anteriores (no max 7) para tentar achar a ultima structure, caso nao ache, entao inicia normalmente
                for(int i = 0; i < 7; i++)
                {
                    var date = today.AddDays(-i);
                    var path = $"{Symbol}_{nameof(StructureService)}_{TimeFrame}MIN_{_minDistanceUpdateBorder}_{date:yyyy-MM-dd}.json";
                    var data = await GetDataAsync(path);
                    if (data is { Count: > 0 })
                    {
                        var last = data.OrderByDescending(q => q.Date).FirstOrDefault();
                        if (last != null)
                        {
                            _lastStructure = last;
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
                $"{Symbol}_{nameof(StructureService)}_{TimeFrame}MIN_{_minDistanceUpdateBorder}_{lastProcessedDate:yyyy-MM-dd}.json";

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
                await SetDataAsync(DataKeep);
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
                DataKeep.Add(result.Clone() as StructureStorageItem);

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
            if (_lastStructure == null)
            {
                if (!skipPreLoad) await PreLoad();

                if (_lastStructure == null)
                {
                    _lastStructure = new StructureStorageItem()
                    {
                        Symbol = newBar.Symbol,
                        Date = newBar.Date,
                        TimeFrame = newBar.TimeFrame,
                        UpBorder = newBar.High,
                        UpAuxBorder = newBar.High,
                        DownBorder = newBar.Low,
                        DownAuxBorder = newBar.Low
                    };
                }

                return _lastStructure;
            }


            //atualiza o date
            _lastStructure.Date = newBar.Date;
            double virtualSameUpAuxBorder = Math.Max(_lastStructure.UpAuxBorder, newBar.High);
            double virtualSameDownAuxBorder = Math.Min(_lastStructure.DownAuxBorder, newBar.Low);

            //se houve um distanciamento entre a borda superior e o preço (rebotando para baixo)
            if (virtualSameUpAuxBorder - newBar.Close >= _minDistanceUpdateBorder && expectBuyDrop)
            {
                //atualiza aux
                _lastStructure.UpAuxBorder = virtualSameUpAuxBorder;

                //atualiza a up
                _lastStructure.UpBorder = _lastStructure.UpAuxBorder;

                //reset
                expectSellDrop = true; expectBuyDrop = false;

                _lastStructure.DownAuxBorder = newBar.Low;

                isSizeChanger = true;
            }

            //se houve um distanciamento entre a borda inferior e o preço (rebotando para cima)
            if (newBar.Close - virtualSameDownAuxBorder >= _minDistanceUpdateBorder && expectSellDrop && !isSizeChanger)
            {
                //atualiza aux
                _lastStructure.DownAuxBorder = virtualSameDownAuxBorder;

                //atualiza a down
                _lastStructure.DownBorder = _lastStructure.DownAuxBorder;

                //reset
                expectSellDrop = false; expectBuyDrop = true;

                _lastStructure.UpAuxBorder = newBar.High;
            }

            _lastStructure.UpAuxBorder = Math.Max(_lastStructure.UpAuxBorder, newBar.High);
            _lastStructure.DownAuxBorder = Math.Min(_lastStructure.DownAuxBorder, newBar.Low);

            isSizeChanger = false;

            return _lastStructure;

        } 

        public async Task SetMinDistance(double minDistance, bool broadcastFinal = true)
        {
            DataKeep = new();
            _lastStructure = null;
            expectBuyDrop = true;
            expectSellDrop = true;
            isSizeChanger = false;
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
        }
    }
}
