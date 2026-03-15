using B3WM.Client.Model;
using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace B3WM.Client.Services
{
    public class CandleHelper : IDisposable
    {
        public event EventHandler<BarStorageItem>? OnClosedBars;
        public event EventHandler<BarStorageItem?>? OnUpdateLastBar;
        public event EventHandler<int>? OnQueueCount;
        public event EventHandler<string>? OnQueueTime;

        private int _timeFrameMinutes;
        private bool _reverseTimeData { get; set; } = false;

        private readonly ConcurrentQueue<Ticks2[]> _queue = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly object _lock = new();
        private BarStorageItem? _currentBar;

        private PeriodicTimer? _timer;

        private int _queueCount { get; set; }

        private string _queueTime { get; set; }

        public void Init(int throtlingms = 200, int timeFrame = 5, bool isReverse = false)
        {
            _timeFrameMinutes = timeFrame;

            _reverseTimeData = isReverse;

            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));
            _ = RunLoop();
        }

        public BarStorageItem? GetCurrentBarSnapshot()
        {
            lock (_lock)
            {
                if (_currentBar == null)
                    return null;

                return CloneBar(_currentBar);
            }
        }

        private async Task RunLoop()
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                OnUpdateLastBar?.Invoke(this, GetCurrentBarSnapshot());
                OnQueueCount?.Invoke(this, _queueCount);
                OnQueueTime?.Invoke(this, _queueTime);
            }
        }

        public void Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return ;

            _queue.Enqueue(ticks);

            _ = ProcessQueueAsync();

            //return Task.CompletedTask;
        }

        private Task ProcessQueueAsync()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                long tickProcessingMs = 0;

                while (_queue.TryDequeue(out var ticks))
                {
                    IList<Ticks2> sortedTicks = new List<Ticks2>();

                    //os dados estão em ordem crescente de tempo
                    if (!_reverseTimeData)
                        sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();
                    else
                        sortedTicks = ticks.OrderByDescending(x => x.Time).ToList();

                    _queueCount = sortedTicks.Count;
                    _queueTime = sortedTicks.Last().Time.ToString("HH:mm:ss");
                    var swTicks = Stopwatch.StartNew();
                    foreach (var t in sortedTicks)
                    {
                        ProcessTick(t);
                    }
                    swTicks.Stop();
                    tickProcessingMs += swTicks.ElapsedMilliseconds;
                }  
            }
            finally
            {
                HelperPerformanceConfig.Log(
                    nameof(CandleHelper),
                    "ProcessQueueAsync",
                    sw.ElapsedMilliseconds,
                    "");
            }

            return Task.CompletedTask;
        }

        private void ProcessTick(Ticks2 t)
        {
            var candleStart = t.Time.GetCandleStart(_timeFrameMinutes);

            BarStorageItem? barToEmit = null;

            lock (_lock)
            {
                if (_currentBar == null)
                {
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume, t.Symbol);
                    return;
                }

                if (candleStart > _currentBar.Date && !_reverseTimeData)
                {
                    // Só fechar quando o tick é de um período posterior (evita fechar por duplicata ou ordem inversa).
                    barToEmit = CloneBar(_currentBar);
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume, t.Symbol);
                }
                else if (candleStart == _currentBar.Date)
                {
                    UpdateBar(_currentBar, t.Value, t.Volume);
                }
                else if (candleStart < _currentBar.Date && _reverseTimeData)
                {
                    // Só fechar quando o tick é de um período posterior (evita fechar por duplicata ou ordem inversa).
                    barToEmit = CloneBar(_currentBar);
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume, t.Symbol);
                }
            }

            // Emitir fora do lock; usamos só a cópia já feita.
            if (barToEmit != null)
            {
                var sw = Stopwatch.StartNew();
                OnClosedBars?.Invoke(this, barToEmit);

                sw.Stop();
            }
        }

        private BarStorageItem CloneBar(BarStorageItem bar)
        {
            return new BarStorageItem
            {
                Date = bar.Date,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume,
                Symbol = bar.Symbol,
                TimeFrame = bar.TimeFrame,
            };
        }

        private BarStorageItem CreateNewBar(DateTime start, double price, long volume, string symbol)
        {
            return new BarStorageItem
            {
                Date = start,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = volume,
                Symbol = symbol,
                TimeFrame = _timeFrameMinutes
            };
        }

        private void UpdateBar(BarStorageItem bar, double price, long volume)
        {
            if (price > bar.High) bar.High = price;
            if (price < bar.Low) bar.Low = price;

            bar.Close = price;
            bar.Volume += volume;
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
