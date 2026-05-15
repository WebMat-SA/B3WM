using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using MudBlazor.Charts;
using System.Diagnostics;
using System.Threading.Channels;

namespace B3WM.Services.Core
{
    public class CandleService : IProcessor<Ticks2, BarStorageItem>
    {
        private readonly IHubContext<DataHub, IDataHubClient> hubContext;

        private readonly Channel<Ticks2[]> _channel =
            Channel.CreateUnbounded<Ticks2[]>();

        public event Func<BarStorageItem, Task>? OnUpdate;

        public int TimeFrame { get; private set; }
        private BarStorageItem? _currentBar;

        public CandleService(int _timeFrame, IHubContext<DataHub, IDataHubClient> hubContext)
        {
            TimeFrame = _timeFrame;
            this.hubContext = hubContext;
            _ = Task.Run(ProcessLoop);
        }

        public void Enqueue(Ticks2[] ticks)
        {
            _channel.Writer.TryWrite(ticks);
            //await Task.CompletedTask;
        }

        public object GetSnapshot() => new
        {
            TimeFrame = TimeFrame,
            CurrentBar = CloneBar(_currentBar?? new BarStorageItem()),
            //QueueCount = _channel.Reader.Count
        };

        private async Task ProcessLoop()
        {
            await foreach (var ticks in _channel.Reader.ReadAllAsync())
            {
                // processamento aqui
                IList<Ticks2> sortedTicks = new List<Ticks2>();

                //os dados estão em ordem crescente de tempo
                sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();

                var swTicks = Stopwatch.StartNew();
                foreach (var t in sortedTicks)
                {
                    await ProcessTick(t);
                }
                swTicks.Stop();

                //Console.WriteLine($"{nameof(CandleService)}:{nameof(ProcessTick)}:{swTicks.ElapsedMilliseconds} ms");
            }
        }

        private async Task ProcessTick(Ticks2 t)
        {
            var candleStart = t.Time.GetCandleStart(TimeFrame);

            BarStorageItem? barToEmit = null;


            if (_currentBar == null)
            {
                _currentBar = CreateNewBar(candleStart, t.Value, t.Volume, t.Symbol);
                return;
            }

            if (candleStart > _currentBar.Date)
            {
                // Só fechar quando o tick é de um período posterior (evita fechar por duplicata ou ordem inversa).
                barToEmit = CloneBar(_currentBar);
                _currentBar = CreateNewBar(candleStart, t.Value, t.Volume, t.Symbol);
            }
            else if (candleStart == _currentBar.Date)
            {
                UpdateBar(_currentBar, t.Value, t.Volume);
            }


            // Emitir fora do lock; usamos só a cópia já feita.
            if (barToEmit != null && OnUpdate != null)
            {
                //ainda pensar sobre signalR e envio de dados para clientes
                if (hubContext != null)
                {
                    await hubContext.Clients.Group(barToEmit.Symbol).ReceiveOnCloseBar(barToEmit);
                }

                await OnUpdate.Invoke(barToEmit);
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
                TimeFrame = TimeFrame
            };
        }

        private void UpdateBar(BarStorageItem bar, double price, long volume)
        {
            if (price > bar.High) bar.High = price;
            if (price < bar.Low) bar.Low = price;

            bar.Close = price;

            bar.Volume += volume;
        }
    }
}
