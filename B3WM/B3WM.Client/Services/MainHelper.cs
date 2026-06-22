using B3WM.Client.Model;
using B3WM.Client.Pages;
using B3WM.Shared.Entity;

using System.Diagnostics;
using System.Globalization;
using Vizor.ECharts;

namespace B3WM.Client.Services
{
    public class MainHelper : IDisposable
    {
        private CandleHelper _candle = new();
        public event EventHandler<BarStorageItem>? Candle_OnClosedBars;
        public event EventHandler<BarStorageItem?>? Candle_OnUpdateLastBar;
        public event EventHandler<int>? Candle_OnQueueCount;
        public event EventHandler<string>? Candle_OnQueueTime;

        private BubbleHelper _bubble = new();
        public event EventHandler<Shared.Models.BubbleStorageItem>? Bubble_OnNewBubble;
        public event EventHandler<int>? Bubble_OnQueueCount;
        public event EventHandler<string>? Bubble_OnQueueTime;

        private VolumeHelper _volume = new();
        public event EventHandler<int>? Volume_OnQueueCount;
        public event EventHandler<VolumeLevelStorageItem>? Volume_OnVolumeUpdate;
        public event EventHandler<string>? Volume_OnQueueTime;

        private StructureHelper _structure = new();
        public event EventHandler <StructureStorageItem>? Structure_OnNewStructure;
        public event EventHandler<int>? Structure_OnQueueCount;
        public event EventHandler<string>? Structure_OnQueueTime;

        private StructureVolumeHelper _structureVolume = new();
        public event EventHandler<StructureVolumeStorageItem>? StructureVolume_OnNewStructure;
        public event EventHandler<int>? StructureVolume_OnQueueCount;
        public event EventHandler<string>? StructureVolume_OnQueueTime;

        private StructureBubbleHelper _structureBubble = new();
        public event EventHandler<StructureBubbleStorageItem>? StructureBubble_OnNewStructure;
        public event EventHandler<int>? StructureBubble_OnQueueCount;
        public event EventHandler<string>? StructureBubble_OnQueueTime;

        private bool EnableCandleFormer { get; set; }
        private bool EnableVolumeFormer { get;set; }
        private bool EnableBubbleFormer { get; set; }
        private bool EnableStructureFormer { get; set; }
        private bool EnableStructureVolumeFormer { get; set; }
        private bool EnableStructureBubbleFormer { get; set; }

        public MainHelper()
        {
            try
            {

                _candle.OnUpdateLastBar += _candle_OnUpdateLastBar;
                _candle.OnClosedBars += _candle_OnClosedBars;
                _candle.OnQueueCount += _candle_OnQueueCount;
                _candle.OnQueueTime += _candle_OnQueueTime;

                _bubble.OnNewBubble += _bubble_OnNewBubble;
                _bubble.OnQueueCount += _bubble_OnQueueCount;
                _bubble.OnQueueTime += _bubble_OnQueueTime;

                _volume.OnVolumeUpdate += _volume_OnVolumeUpdate;
                _volume.OnQueueCount += _volume_OnQueueCount;
                _volume.OnQueueTime += _volume_OnQueueTime;

                _structure.OnQueueCount += _structure_OnQueueCount;
                _structure.OnQueueTime += _structure_OnQueueTime;
                _structure.OnStructureChange += _structure_OnNewStructure;

                _structureVolume.OnQueueCount += _structureVolume_OnQueueCount;
                _structureVolume.OnQueueTime += _structureVolume_OnQueueTime;
                _structureVolume.OnStructureChange += _structureVolume_OnNewStructure;

                _structureBubble.OnQueueCount += _structureBubble_OnQueueCount;
                _structureBubble.OnQueueTime += _structureBubble_OnQueueTime;
                _structureBubble.OnStructureChange += _structureBubble_OnNewStructure;
            }
            catch (Exception ex)
            {
                HelperPerformanceConfig.Log(nameof(MainHelper), nameof(MainHelper), 0, $"Error initializing MainHelper: {ex.Message}");
            }
        }

        #region Binding
        private void _volume_OnVolumeUpdate(object? sender, VolumeLevelStorageItem e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_volume_OnVolumeUpdate), 0, $"Volume update received: {e.Date} levels");
            if (Volume_OnVolumeUpdate != null) Volume_OnVolumeUpdate.Invoke(this, e);

            if (EnableStructureVolumeFormer)
                _ = _structureVolume.Enqueu(e);
        }

        private void _bubble_OnNewBubble(object? sender, Shared.Models.BubbleStorageItem e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_bubble_OnNewBubble), 0, $"New bubble: {e}");
            if (Bubble_OnNewBubble != null) Bubble_OnNewBubble.Invoke(this, e);
        }

        private void _candle_OnClosedBars(object? sender, BarStorageItem e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnClosedBars), 0, $"Closed bars received: {e.Date}");
            if (Candle_OnClosedBars != null) Candle_OnClosedBars.Invoke(this, e);

            if (EnableStructureFormer && !_structure.calculatingNewDistance)
                _ = _structure.OnNewBar(e);

            if (EnableStructureBubbleFormer && !_structureBubble.calculatingNewDistance)
                _ = _structureBubble.OnNewBar(e);
        }

        private void _candle_OnUpdateLastBar(object? sender, BarStorageItem? e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnUpdateLastBar), 0,$"Last bar updated: {e}");
            if (Candle_OnUpdateLastBar != null) Candle_OnUpdateLastBar.Invoke(this, e);
        }

        private void _structure_OnNewStructure(object? sender, StructureStorageItem? e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structure_OnNewStructure), 0, $"Structure updated: {e}");
            if (Structure_OnNewStructure != null) Structure_OnNewStructure.Invoke(this, e);
        }

        private void _structureVolume_OnNewStructure(object? sender, StructureVolumeStorageItem? e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structureVolume_OnNewStructure), 0, $"Structure updated: {e}");
            if (StructureVolume_OnNewStructure != null) StructureVolume_OnNewStructure.Invoke(this, e);
        }

        private void _structureBubble_OnNewStructure(object? sender, StructureBubbleStorageItem? e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structureBubble_OnNewStructure), 0, $"Structure Bubble updated: {e}");
            if (StructureBubble_OnNewStructure != null) StructureBubble_OnNewStructure.Invoke(this, e);
        }
        #region Init

        public void InitCandle(int throtlingms = 200, int timeFrame = 5, bool _enableCandleFormer = true)
        {
            EnableCandleFormer = _enableCandleFormer;
            _candle.Init(throtlingms, timeFrame);

            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(InitCandle), 0, "Candle helper initialized");
        }

        public void InitBubble(int throtlingms = 200, int bubbleThreshold = 125, bool _enableBubbleFormer = true)
        {
            EnableBubbleFormer = _enableBubbleFormer;
            _bubble.Init(throtlingms, bubbleThreshold);

            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(InitBubble), 0, "Bubble helper initialized");
        }

        public void InitVolume(int throtlingms = 200, bool _enableVolumeFormer = true)
        {
            EnableVolumeFormer = _enableVolumeFormer;
            _volume.Init(throtlingms);

            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(InitVolume), 0, "Volume helper initialized");
        }

        public void InitStructure(int throtlingms = 5000, double minDistanceToUpd = 250, bool _enableStructureFormer = true)
        {
            EnableStructureFormer = _enableStructureFormer;
            _structure.Init(throtlingms,minDistanceToUpd);

            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(InitStructure), 0, "Structure helper initialized");
        }

        public void InitStructureVolume(int throtlingms = 5000, double alpha = 0.2,bool _enableStructureFormer = true)
        {
            EnableStructureVolumeFormer = _enableStructureFormer;
            _structureVolume.Init(throtlingms, alpha);

            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(InitStructureVolume), 0, "Structure helper initialized");
        }

        public void InitStructureBubble(int throtlingms = 5000, bool _enableStructureBubbFormer = true)
        {
            EnableStructureBubbleFormer = _enableStructureBubbFormer;
            //_structureBubble.Init(throtlingms);

            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(InitStructureBubble), 0, "Structure Bubble helper initialized");
        }
        #endregion

        #region Queue Count
        private void _volume_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_volume_OnQueueCount), 0, $"Volume queue count: {e}");
            if (Volume_OnQueueCount != null) Volume_OnQueueCount.Invoke(this, e);
        }
        private void _bubble_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_bubble_OnQueueCount), 0, $"Bubble queue count: {e}");
            if (Bubble_OnQueueCount != null) Bubble_OnQueueCount.Invoke(this, e);
        }
        private void _candle_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnQueueCount), 0, $"Candle queue count: {e}");
            if (Candle_OnQueueCount != null) Candle_OnQueueCount.Invoke(this, e);
        }
        private void _structure_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structure_OnQueueCount), 0, $"Structure queue count: {e}");
            if (Structure_OnQueueCount != null) Structure_OnQueueCount.Invoke(this, e);
        }
        private void _structureVolume_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structureVolume_OnQueueCount), 0, $"Structure Volume queue count: {e}");
            if (StructureVolume_OnQueueCount != null) StructureVolume_OnQueueCount.Invoke(this, e);
        }
        private void _structureBubble_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structureBubble_OnQueueCount), 0, $"Structure Bubble queue count: {e}");
            if (StructureBubble_OnQueueCount != null) StructureBubble_OnQueueCount.Invoke(this, e);
        }
        #endregion

        #region Queue Time
        private void _volume_OnQueueTime(object? sender, string e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_volume_OnQueueTime), 0, $"Volume queue time: {e}");
            if (Volume_OnQueueTime != null) Volume_OnQueueTime.Invoke(this, e);
        }

        private void _bubble_OnQueueTime(object? sender, string e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_bubble_OnQueueTime), 0, $"Bubble queue time: {e}");
            if (Bubble_OnQueueTime != null) Bubble_OnQueueTime.Invoke(this, e);
        }

        private void _candle_OnQueueTime(object? sender, string e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnQueueTime), 0, $"Candle queue time: {e}");
            if (Candle_OnQueueTime != null) Candle_OnQueueTime.Invoke(this, e);
        }

        private void _structure_OnQueueTime(object? sender, string e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structure_OnQueueTime), 0, $"Structure queue time: {e}");
            if (Structure_OnQueueTime != null) Structure_OnQueueTime.Invoke(this, e);
        }

        private void _structureVolume_OnQueueTime(object? sender, string e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structureVolume_OnQueueTime), 0, $"Structure Volume queue time: {e}");
            if (StructureVolume_OnQueueTime != null) StructureVolume_OnQueueTime.Invoke(this, e);
        }

        private void _structureBubble_OnQueueTime(object? sender, string e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_structureBubble_OnQueueTime), 0, $"Structure Bubble queue time: {e}");
            if (StructureBubble_OnQueueTime != null) StructureBubble_OnQueueTime.Invoke(this, e);
        }
        #endregion

        #endregion

        #region Formers
        public void SetEnableCandleFormer(bool enable)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(SetEnableCandleFormer), 0, $"Candle former enabled: {enable}");
            EnableCandleFormer = enable;
        }

        public void SetEnableBubbleFormer(bool enable)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(SetEnableBubbleFormer), 0, $"Bubble former enabled: {enable}");
            EnableBubbleFormer = enable;
        }

        public void SetEnableVolumeFormer(bool enable)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(SetEnableVolumeFormer), 0, $"Volume former enabled: {enable}");
            EnableVolumeFormer = enable;
        }

        public void SetEnableStructureFormer(bool enable)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(SetEnableStructureFormer), 0, $"Structure former enabled: {enable}");
            EnableStructureFormer = enable;
        }

        public void SetEnableStructureVolumeFormer(bool enable)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(SetEnableStructureVolumeFormer), 0, $"Structure Volume former enabled: {enable}");
            EnableStructureVolumeFormer = enable;
        }

        public void SetEnableStructureBubbleFormer(bool enable)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(SetEnableStructureBubbleFormer), 0, $"Structure Bubble former enabled: {enable}");
            EnableStructureBubbleFormer = enable;
        }
        #endregion

        public void Enqueue(string dataString)
        {
            var sw = Stopwatch.StartNew();

            // Parse único: todos os helpers e subscribers recebem a mesma lista
            var ticks = new DataHelper(dataString).TimesAndTrades().ToArray();

            Enqueue(ticks);

            sw.Stop();
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(Enqueue), sw.ElapsedMilliseconds, $"Ticks received: {ticks.Length}");
        }
        public void EnqueueProfit(string dataStringTicks2)
        {
            var sw = Stopwatch.StartNew();

            // Parse único: todos os helpers e subscribers recebem a mesma lista
            var ticks = System.Text.Json.JsonSerializer.Deserialize<Ticks2[]>(dataStringTicks2);

            //envia para os services
            if (ticks != null)
                Enqueue(ticks);

            sw.Stop();
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(Enqueue), sw.ElapsedMilliseconds, $"Ticks received: {ticks.Length}");
        }

        public void EnqueueFromCsv(string ticksString, DateTime Date, string Symbol)
        {
            var sw = Stopwatch.StartNew();

            //parse do json
            var listData = System.Text.Json.JsonSerializer.Deserialize<List<Ticks2>>(ticksString);

            if (listData == null || listData.Count <= 0) return;
            Enqueue(listData.ToArray());

            sw.Stop();
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(Enqueue), sw.ElapsedMilliseconds, $"Ticks received: {listData.Count}");
        }

        private void Enqueue(Ticks2[] ticks)
        {
            if (ticks.Length == 0) return;

            if (EnableCandleFormer) _candle.Enqueue(ticks);
            if (EnableBubbleFormer) _bubble.Enqueue(ticks);
            if (EnableVolumeFormer) _volume.Enqueue(ticks);
        }


        public void SetVolumeIntraday(string Jsonvolumes)
        {
            var volumes = System.Text.Json.JsonSerializer.Deserialize<List<VolumeLevel>>(Jsonvolumes);

            _volume.AddIntradayVolume(volumes ?? new());
        }

        public async Task<List<StructureStorageItem>> SetMinDistanceStrucure(double minDistanceUpdateBorder, string jsonListBar)
        {
            return await _structure.SetMinDistance(minDistanceUpdateBorder, jsonListBar);
        }

        public void SetFttStructureVolume(double _alpha)
        {
            _structureVolume.SetFftStrength(_alpha);
        }

        public void Dispose()
        {
            _candle.OnUpdateLastBar -= Candle_OnUpdateLastBar;
            _candle.OnClosedBars -= Candle_OnClosedBars;
            _candle.OnQueueCount -= Candle_OnQueueCount;

            _bubble.OnNewBubble -= Bubble_OnNewBubble;
            _bubble.OnQueueCount -= Bubble_OnQueueCount;   
            
            _volume.OnVolumeUpdate -= Volume_OnVolumeUpdate;
            _volume.OnQueueCount -= Volume_OnQueueCount;

            _candle.Dispose();
            _bubble.Dispose();
            _volume.Dispose();
        }
    }
}
