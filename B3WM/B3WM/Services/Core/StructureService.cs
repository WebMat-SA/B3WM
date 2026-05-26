using B3WM.Client.Services;
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

        public event Func<StructureStorageItem, Task>? OnUpdate;

        public override string Path => $"{Symbol}_{nameof(StructureService)}_{TimeFrame}MIN_{_minDistanceUpdateBorder}_{DateTime.Now:yyyy-MM-dd}.json";

        //variaveis de memoria
        StructureStorageItem? _lastStructure { get; set; }

        #region Calculating
        public double _minDistanceUpdateBorder { get; set; }
        bool expectBuyDrop = true;
        bool expectSellDrop = true;
        bool isSizeChanger = false;
        #endregion


        public StructureService(string symbol, int timeFrame, double minDistance ,IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            Symbol = symbol;
            TimeFrame = timeFrame;
            _minDistanceUpdateBorder = minDistance;
            this.hubContext = hubContext;
        }

        public async Task PreLoad()
        {
            //fazer aqui, carregar dos arquivos do Data, para saber a ultima structure (mesmo sendo do dia anterior)
            var allData = await GetDataAsync();

            DataKeep = allData;

            if (allData != null && allData.Count > 0)
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
                    var date = DateTime.Now.AddDays(-i);
                    var path = $"{Symbol}_{nameof(StructureService)}_{TimeFrame}MIN_{_minDistanceUpdateBorder}_{date:yyyy-MM-dd}.json";
                    var data = await GetDataAsync(path);
                    if (data != null)
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
                await Regenerate();
            }
        }

        public async Task Regenerate()
        {
            var candleService = _serviceProvider.GetServices<CandleService>().FirstOrDefault(q=>q.Symbol == Symbol && q.TimeFrame == TimeFrame);

            if (candleService == null)
            {
                throw new Exception($"CandleService for symbol {Symbol} and timeframe {TimeFrame} not found.");
            }

            //buscar todos os candles dos ultimos 7 dias, gerar a estrutura e salvar
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Now.AddDays(-i);
                var candlePath = $"{Symbol}_{nameof(CandleService)}_{TimeFrame}MIN_{date:yyyy-MM-dd}.json";
                var candles = await candleService.GetDataAsync(candlePath);
                if (candles != null)
                {
                    foreach (var candle in candles)
                    {
                        if (candle != null) await Calculate(candle, true);
                    }
                }
            }
        }

        public async Task<StructureStorageItem> Calculate(BarStorageItem newBar, bool skipPreLoad = false)
        {
            var result = await Generate(newBar, skipPreLoad);

            //ainda pensar sobre signalR e envio de dados para clientes
            if (hubContext != null)
            {
                await hubContext.Clients.Group(Symbol).ReceiveOnStructure(result);
            }

            if (OnUpdate != null) await OnUpdate.Invoke(result);

            DataKeep.Add(result.Clone() as StructureStorageItem);

            await SetDataAsync(DataKeep);

            return result;
        }

        private async Task<StructureStorageItem> Generate(BarStorageItem newBar, bool skipPreLoad = false)
        {
            //HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"OnNewBar");
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

            //HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(OnNewBar), 0, $"MinDist:{_minDistanceUpdateBorder} | Diff Up|Close: {virtualSameUpAuxBorder - newBar.Close}");

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

            //HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(OnNewBar), 0, $"MinDist:{_minDistanceUpdateBorder} | Diff Down|Close: {newBar.Close - virtualSameDownAuxBorder}");

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

        public async Task SetMinDistance(double minDistance)
        {
            DataKeep = new();
            _lastStructure = null;
            expectBuyDrop = true;
            expectSellDrop = true;
            isSizeChanger = false;
            _minDistanceUpdateBorder = minDistance;

            await Regenerate();
        }
    }
}
