using B3WM.Client.Model;
using System.Timers;

namespace B3WM.Client.Services
{
    public class StructureHelper
    {
        public event EventHandler<StructureStorageItem>? OnStructureChange;
        public event EventHandler<int>? OnQueueCount;
        public event EventHandler<string>? OnQueueTime;


        private int _queueCount { get; set; }
        private string _queueTime { get; set; }

        private PeriodicTimer? _timer;
        public double _minDistanceUpdateBorder { get; set; }

        public void Init(int throtlingms = 5000, double minDistanceUpdateBorder = 250)
        {
            _minDistanceUpdateBorder = minDistanceUpdateBorder;

            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));
            _ = RunLoop();

            HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"Init => minDist:{minDistanceUpdateBorder}");
        }

        public void SetMinDistance(double minDistanceUpdateBorder)
        {
            _minDistanceUpdateBorder = minDistanceUpdateBorder;
        }

        private async Task RunLoop()
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                OnQueueCount?.Invoke(this, _queueCount);
                OnQueueTime?.Invoke(this, _queueTime);
            }
        }

        //variaveis de memoria
        StructureStorageItem? _lastStructure { get; set; }

        bool expectBuyDrop = true;
        bool expectSellDrop = true;
        bool isSizeChanger = false;

        public Task OnNewBar(BarStorageItem newBar)
        {
            //HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"OnNewBar");
            //se for primeiro candle
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

                HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"First _lastStructure: {_lastStructure.ToString()}");

                if (OnStructureChange != null) OnStructureChange.Invoke(this, _lastStructure);

                return Task.CompletedTask;
            }

            //atualiza o date
            _lastStructure.Date = newBar.Date;
            double virtualSameUpAuxBorder = Math.Max(_lastStructure.UpAuxBorder, newBar.High);
            double virtualSameDownAuxBorder = Math.Min(_lastStructure.DownAuxBorder, newBar.Low);

            HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"MinDist:{_minDistanceUpdateBorder} | Diff Up|Close: {virtualSameUpAuxBorder - newBar.Close}");

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

            HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"MinDist:{_minDistanceUpdateBorder} | Diff Down|Close: {newBar.Close - virtualSameDownAuxBorder}");

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
            //if (resetAuxDown)
            //    if (newBar.Close > _lastStructure.DownBorder) _lastStructure.DownAuxBorder = newBar.Low;
            //    else _lastStructure.DownAuxBorder = virtualSameDownAuxBorder;

            //if (resetAuxUp)
            //    if (newBar.Close < _lastStructure.UpBorder) _lastStructure.UpAuxBorder = newBar.High;
            //    else _lastStructure.UpAuxBorder = virtualSameUpAuxBorder;

            HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"UpAuxBorder: {_lastStructure.UpAuxBorder.ToString("0.00")} | UpBorder: {_lastStructure.UpBorder.ToString("0.00")}");
            HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(Init), 0, $"DownAuxBorder: {_lastStructure.DownAuxBorder.ToString("0.00")} | DownBorder: {_lastStructure.DownBorder.ToString("0.00")}");

            if (OnStructureChange != null) OnStructureChange.Invoke(this, _lastStructure);

            return Task.CompletedTask;
        }

    }
}
