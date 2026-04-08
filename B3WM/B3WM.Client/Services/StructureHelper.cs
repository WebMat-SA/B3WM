using B3WM.Client.Model;
using System.Collections;
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

        public bool calculatingNewDistance { get; set; }

        public async Task<List<StructureStorageItem>> SetMinDistance(double minDistanceUpdateBorder, string jsonListBars)
        {
            if (jsonListBars == null)
                return new();


            List<StructureStorageItem> structures = new List<StructureStorageItem>();

            calculatingNewDistance = true;
            try
            {
                _minDistanceUpdateBorder = minDistanceUpdateBorder;
                _lastStructure = null;

                HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(SetMinDistance), 0, $"SetMinDistance => minDist:{minDistanceUpdateBorder} | jsonListBars: {jsonListBars}");

                List<BarStorageItem> bars = System.Text.Json.JsonSerializer.Deserialize<List<BarStorageItem>>(jsonListBars);

                foreach(var bar in bars.OrderBy(e=>e.Date))
                {
                    await OnNewBar(bar);

                    structures.Add(new StructureStorageItem() {
                        Date = _lastStructure.Date,
                        DownAuxBorder = _lastStructure.DownAuxBorder,
                        UpAuxBorder = _lastStructure.UpAuxBorder,
                        DownBorder = _lastStructure.DownBorder,
                        UpBorder = _lastStructure.UpBorder,
                        Symbol = _lastStructure.Symbol,
                        TimeFrame = _lastStructure.TimeFrame,
                    });
                }
            }
            finally
            {
                calculatingNewDistance = false;
            }

            HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(SetMinDistance), 0, $"_lastStructure: {structures.Count}");

            return structures;
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

                HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(OnNewBar), 0, $"First _lastStructure: {_lastStructure.ToString()}");

                if (OnStructureChange != null) OnStructureChange.Invoke(this, _lastStructure);

                _queueCount = 0;
                _queueTime = DateTime.Now.ToString("HH:mm:ss");

                return Task.CompletedTask;
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

            HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(OnNewBar), 0, $"UpAuxBorder: {_lastStructure.UpAuxBorder.ToString("0.00")} | UpBorder: {_lastStructure.UpBorder.ToString("0.00")}");
            //HelperPerformanceConfig.Log(nameof(StructureHelper), nameof(OnNewBar), 0, $"DownAuxBorder: {_lastStructure.DownAuxBorder.ToString("0.00")} | DownBorder: {_lastStructure.DownBorder.ToString("0.00")}");

            if (OnStructureChange != null) OnStructureChange.Invoke(this, _lastStructure);

            _queueCount = 0;
            _queueTime = DateTime.Now.ToString("HH:mm:ss");

            return Task.CompletedTask;
        }

    }
}
