using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using BlazorWorker.WorkerBackgroundService;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Vizor.ECharts;

namespace B3WM.Client.Services
{
    public class CandleHelper : IDisposable
    {
        public event EventHandler<IEnumerable<Bars>>? OnClosedBars;
        public event EventHandler<Bars?>? OnUpdateLastBar;
        public event EventHandler<int>? OnQueueCount;
        
        private int _timeFrameMinutes;

        private readonly ConcurrentQueue<Ticks2[]> _queue = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly object _lock = new();
        private Bars? _currentBar;

        private PeriodicTimer? _timer;

        private int _queueCount { get; set; }

        public void Init(int throtlingms = 200, int timeFrame = 5)
        {
            _timeFrameMinutes = timeFrame;

            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));
            _ = RunLoop();
        }

        public Bars? GetCurrentBarSnapshot()
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
            }
        }

        public Task Enqueue(Ticks2[] ticks)
        {
            if (ticks == null || ticks.Length == 0) return Task.CompletedTask;

            _queue.Enqueue(ticks);

            _ = Task.Run(ProcessQueueAsync, _cts.Token);

            return Task.CompletedTask;
        }

        /// <summary>Processa ticks de forma síncrona (para load do IndexedDB). Emite barras fechadas via callback. Não usa fila.</summary>
        public void ProcessTicksSync(IReadOnlyList<Ticks2> ticks)
        {
            if (ticks == null || ticks.Count == 0) return;
            foreach (var t in ticks.OrderBy(x => x.Time))
                ProcessTick(t);
        }

        private void ProcessQueueAsync()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                long tickProcessingMs = 0;

                while (_queue.TryDequeue(out var ticks))
                {
                    var sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();

                    _queueCount = sortedTicks.Count;
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
        }

        private void ProcessTick(Ticks2 t)
        {
            var candleStart = t.Time.GetCandleStart(_timeFrameMinutes);

            Bars? barToEmit = null;

            lock (_lock)
            {
                if (_currentBar == null)
                {
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume);
                    return;
                }

                if (candleStart > _currentBar.Date)
                {
                    // Só fechar quando o tick é de um período posterior (evita fechar por duplicata ou ordem inversa).
                    barToEmit = CloneBar(_currentBar);
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume);
                }
                else if (candleStart == _currentBar.Date)
                {
                    UpdateBar(_currentBar, t.Value, t.Volume);
                }
                // candleStart < _currentBar.Date: tick do passado (ordem inversa/atrasado), ignorar
            }

            // Emitir fora do lock; usamos só a cópia já feita.
            if (barToEmit != null)
            {
                var sw = Stopwatch.StartNew();
                OnClosedBars?.Invoke(this, new[] { barToEmit });
                sw.Stop();
            }
        }

        private Bars CloneBar(Bars bar)
        {
            return new Bars
            {
                Date = bar.Date,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume
            };
        }

        private Bars CreateNewBar(DateTime start, double price, long volume)
        {
            return new Bars
            {
                Date = start,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = volume
            };
        }

        private void UpdateBar(Bars bar, double price, long volume)
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
