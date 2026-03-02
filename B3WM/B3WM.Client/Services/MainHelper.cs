using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using System.Diagnostics;

namespace B3WM.Client.Services
{
    public class MainHelper : IDisposable
    {
        private CandleHelper _candle = new();
        public event EventHandler<IEnumerable<Bars>>? Candle_OnClosedBars;
        public event EventHandler<Bars?>? Candle_OnUpdateLastBar;
        public event EventHandler<int>? Candle_OnQueueCount;

        private BubbleHelper _bubble = new();
        public event EventHandler<Bubble>? Bubble_OnNewBubble;
        public event EventHandler<int>? Bubble_OnQueueCount;

        private VolumeHelper _volume = new();
        public event EventHandler<int>? Volume_OnQueueCount;
        public event EventHandler<List<VolumeLevel>>? Volume_OnVolumeUpdate;

        public bool EnableCandleFormer { get; set; }
        public bool EnableVolumeFormer { get;set; }
        public bool EnableBubbleFormer { get;set; }

        public MainHelper()
        {
            _candle.OnUpdateLastBar += _candle_OnUpdateLastBar;
            _candle.OnClosedBars += _candle_OnClosedBars;
            _candle.OnQueueCount += _candle_OnQueueCount;

            _bubble.OnNewBubble += _bubble_OnNewBubble;
            _bubble.OnQueueCount += _bubble_OnQueueCount;

            _volume.OnVolumeUpdate += _volume_OnVolumeUpdate;
            _volume.OnQueueCount += _volume_OnQueueCount;
        }

        #region Binding

        private void _volume_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_volume_OnQueueCount), 0, $"Volume queue count: {e}");
            if (Volume_OnQueueCount != null) Volume_OnQueueCount.Invoke(this, e);
        }

        private void _volume_OnVolumeUpdate(object? sender, List<VolumeLevel> e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_volume_OnVolumeUpdate), 0, $"Volume update received: {e.Count} levels");
            if (Volume_OnVolumeUpdate != null) Volume_OnVolumeUpdate.Invoke(this, e);
        }

        private void _bubble_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_bubble_OnQueueCount), 0, $"Bubble queue count: {e}");
            if (Bubble_OnQueueCount != null) Bubble_OnQueueCount.Invoke(this, e);
        }

        private void _bubble_OnNewBubble(object? sender, Bubble e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_bubble_OnNewBubble), 0, $"New bubble: {e}");
            if (Bubble_OnNewBubble != null) Bubble_OnNewBubble.Invoke(this, e);
        }

        private void _candle_OnQueueCount(object? sender, int e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnQueueCount), 0, $"Candle queue count: {e}");
            if (Candle_OnQueueCount != null) Candle_OnQueueCount.Invoke(this, e);
        }

        private void _candle_OnClosedBars(object? sender, IEnumerable<Bars> e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnClosedBars), 0, $"Closed bars received: {e.Count()}");
            if (Candle_OnClosedBars != null) Candle_OnClosedBars.Invoke(this, e);
        }

        private void _candle_OnUpdateLastBar(object? sender, Bars? e)
        {
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(_candle_OnUpdateLastBar), 0,$"Last bar updated: {e}");
            if (Candle_OnUpdateLastBar != null) Candle_OnUpdateLastBar.Invoke(this, e);
        }

        public void InitCandle(int throtlingms = 200, int timeFrame = 5, bool _notifyqueue = true, bool _enableCandleFormer = true)
        {
            EnableCandleFormer = _enableCandleFormer;
            _candle.Init(throtlingms, timeFrame, _notifyqueue);
        }

        public void InitBubble(int throtlingms = 200, int bubbleThreshold = 125, bool _notifyqueue = true, bool _enableBubbleFormer = true)
        {
            EnableBubbleFormer = _enableBubbleFormer;
            _bubble.Init(throtlingms, bubbleThreshold, _notifyqueue);

            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(InitBubble), 0, $"BubbleHelper initialized with throtlingms={throtlingms}, bubbleThreshold={bubbleThreshold}, notifyQueue={_notifyqueue}, enableBubbleFormer={_enableBubbleFormer}");
        }

        public void InitVolume(int throtlingms = 200, bool _notifyqueue = true, bool _enableVolumeFormer = true)
        {
            EnableVolumeFormer = _enableVolumeFormer;
            _volume.Init(throtlingms, _notifyqueue);
        }

        #endregion

        public void SetEnableCandleFormer(bool enable)
        {
            EnableCandleFormer = enable;
        }

        public void SetEnableBubbleFormer(bool enable)
        {
            EnableBubbleFormer = enable;
        }

        public void SetEnableVolumeFormer(bool enable)
        {
            EnableVolumeFormer = enable;
        }

        public void Enqueue(Ticks2[] ticks)
        {
            var sw = Stopwatch.StartNew();

            if (EnableCandleFormer) _candle.Enqueue(ticks);
            if (EnableBubbleFormer) _bubble.Enqueue(ticks);
            if (EnableVolumeFormer) _volume.Enqueue(ticks);

            sw.Stop();
            HelperPerformanceConfig.Log(nameof(MainHelper), nameof(Enqueue), sw.ElapsedMilliseconds, $"Ticks received: {ticks.Length}");
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
