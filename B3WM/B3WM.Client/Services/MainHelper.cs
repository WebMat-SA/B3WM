using B3WM.Client.Model;
using B3WM.Client.Pages;
using B3WM.Shared.Entity;

using System.Diagnostics;

namespace B3WM.Client.Services
{
    public class MainHelper : IDisposable
    {
        private CandleHelper _candle = new();
        public event EventHandler<IEnumerable<BarStorageItem>>? Candle_OnClosedBars;
        public event EventHandler<BarStorageItem?>? Candle_OnUpdateLastBar;
        public event EventHandler<int>? Candle_OnQueueCount;
        public event EventHandler<string>? Candle_OnQueueTime;

        private BubbleHelper _bubble = new();
        public event EventHandler<BubbleStorageItem>? Bubble_OnNewBubble;
        public event EventHandler<int>? Bubble_OnQueueCount;
        public event EventHandler<string>? Bubble_OnQueueTime;

        private VolumeHelper _volume = new();
        public event EventHandler<int>? Volume_OnQueueCount;
        public event EventHandler<VolumeLevelStorageItem>? Volume_OnVolumeUpdate;
        public event EventHandler<string>? Volume_OnQueueTime;

        private bool EnableCandleFormer { get; set; }
        private bool EnableVolumeFormer { get;set; }
        private bool EnableBubbleFormer { get; set; }

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

            }
            catch (Exception ex)
            {
                HelperPerformanceConfig.Log(nameof(MainHelper), nameof(MainHelper), 0, $"Error initializing MainHelper: {ex.Message}");
            }
        }

        #region Binding

        private void _volume_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_volume_OnQueueCount), 0, $"Volume queue count: {e}");
            if (Volume_OnQueueCount != null) Volume_OnQueueCount.Invoke(this, e);
        }

        private void _volume_OnVolumeUpdate(object? sender, VolumeLevelStorageItem e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_volume_OnVolumeUpdate), 0, $"Volume update received: {e.Date} levels");
            if (Volume_OnVolumeUpdate != null) Volume_OnVolumeUpdate.Invoke(this, e);
        }

        private void _bubble_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_bubble_OnQueueCount), 0, $"Bubble queue count: {e}");
            if (Bubble_OnQueueCount != null) Bubble_OnQueueCount.Invoke(this, e);
        }

        private void _bubble_OnNewBubble(object? sender, BubbleStorageItem e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_bubble_OnNewBubble), 0, $"New bubble: {e}");
            if (Bubble_OnNewBubble != null) Bubble_OnNewBubble.Invoke(this, e);
        }

        private void _candle_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnQueueCount), 0, $"Candle queue count: {e}");
            if (Candle_OnQueueCount != null) Candle_OnQueueCount.Invoke(this, e);
        }

        private void _candle_OnClosedBars(object? sender, IEnumerable<BarStorageItem> e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnClosedBars), 0, $"Closed bars received: {e.Count()}");
            if (Candle_OnClosedBars != null) Candle_OnClosedBars.Invoke(this, e);
        }

        private void _candle_OnUpdateLastBar(object? sender, BarStorageItem? e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnUpdateLastBar), 0,$"Last bar updated: {e}");
            if (Candle_OnUpdateLastBar != null) Candle_OnUpdateLastBar.Invoke(this, e);
        }

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

        #endregion

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

        public void Enqueue(string dataString)
        {
            var sw = Stopwatch.StartNew();

            // Parse único: todos os helpers e subscribers recebem a mesma lista
            var ticks = new DataHelper(dataString).TimesAndTrades().ToArray();

            Enqueue(ticks);

            sw.Stop();
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(Enqueue), sw.ElapsedMilliseconds, $"Ticks received: {ticks.Length}");
        }

        public void EnqueueFromCsv(string ticksString)
        {
            var sw = Stopwatch.StartNew();

            //parse do json
            var ticks = System.Text.Json.JsonSerializer.Deserialize<Ticks2[]>(ticksString);

            if (ticks != null) Enqueue(ticks);

            sw.Stop();
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(Enqueue), sw.ElapsedMilliseconds, $"Ticks received: {ticks.Length}");
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
