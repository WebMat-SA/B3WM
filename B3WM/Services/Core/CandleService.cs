using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Threading.Channels;

namespace B3WM.Services.Core
{
    public class CandleService : DataKeeperService<List<BarStorageItem>>, IProcessor<Ticks2, BarStorageItem>, ISymbolable
    {
        public string Symbol { get; }
        public int TimeFrame { get; private set; }

        private readonly IHubContext<DataHub, IDataHubClient> hubContext;

        private readonly Channel<Ticks2[]> _channel =
            Channel.CreateUnbounded<Ticks2[]>();

        public event Func<BarStorageItem, Task>? OnUpdate;


        private BarStorageItem? _currentBar;

        private readonly object _barLock = new();
        private DateTime _loadedDate;
        private readonly PeriodicTimer _flushTimer = new(TimeSpan.FromMinutes(1));

        public override string Path => GetPathForDate(DateTime.Now);

        public string GetPathForDate(DateTime date) => $"{Symbol}_{nameof(CandleService)}_{TimeFrame}MIN_{date:yyyy-MM-dd}.json";

        public CandleService(string symbol, int timeFrame, IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
            Symbol = symbol;
            TimeFrame = timeFrame;
            this.hubContext = hubContext;
            _loadedDate = DateTime.Now.Date;
            _ = Task.Run(ProcessLoop);
            // Flush periódico só para o diário: o candle aberto vive o pregão inteiro
            // só em memória e seria perdido sem escrita até o fechamento (virada do dia).
            if (timeFrame == 1440)
                _ = Task.Run(FlushLoop);
        }

        public void Enqueue(Ticks2[] ticks)
        {
            _channel.Writer.TryWrite(ticks);
            //await Task.CompletedTask;
        }

        public BarStorageItem GetSnapshot()
        {
            lock (_barLock)
            {
                return CloneBar(_currentBar ?? new BarStorageItem());
            }
        }

        private async Task ProcessLoop()
        {
            // se houver arquivo no sistema com a especificação desse serviço, já carrega na memória para evitar perda de dados.
            await LoadAsync();
            _loadedDate = DateTime.Now.Date;
            if (DataKeep == null)
                DataKeep = new List<BarStorageItem>();

            // Diário: restaura a barra aberta de hoje a partir do flush anterior,
            // senão um restart no meio do pregão descartaria o progresso do dia.
            if (TimeFrame == 1440 && DataKeep.Count > 0)
            {
                try
                {
                    var today = DateTime.Now.Date;
                    var todaysBar = DataKeep
                        .Where(b => b.Date.Date == today)
                        .OrderByDescending(b => b.Date)
                        .FirstOrDefault();
                    if (todaysBar != null)
                    {
                        lock (_barLock)
                        {
                            _currentBar = CloneBar(todaysBar);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CandleService restore 1440 error: {ex.Message}");
                }
            }

            await foreach (var ticks in _channel.Reader.ReadAllAsync())
            {
                try
                {
                    IList<Ticks2> sortedTicks = ticks.OrderBy(x => x.Time).ThenBy(x => x.TrydID).ToList();

                    var swTicks = Stopwatch.StartNew();
                    foreach (var t in sortedTicks)
                    {
                        await ProcessTick(t);
                    }
                    swTicks.Stop();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CandleService.ProcessLoop error: {ex.Message}");
                }
            }
        }

        private async Task ProcessTick(Ticks2 t)
        {
            var candleStart = t.Time.GetCandleStart(TimeFrame);

            BarStorageItem? barToEmit = null;
            bool isNewBar = false;

            lock (_barLock)
            {
                if (_currentBar == null)
                {
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume, t.Symbol);
                    isNewBar = true;
                }
                else if (candleStart > _currentBar.Date)
                {
                    // Só fechar quando o tick é de um período posterior (evita fechar por duplicata ou ordem inversa).
                    barToEmit = CloneBar(_currentBar);
                    _currentBar = CreateNewBar(candleStart, t.Value, t.Volume, t.Symbol);
                }
                else if (candleStart == _currentBar.Date)
                {
                    UpdateBar(_currentBar, t.Value, t.Volume);
                }
            }

            // Diário: persiste a barra recém-criada de imediato para o arquivo do dia
            // não ficar vazio até o flush de 1min.
            if (isNewBar)
            {
                if (TimeFrame == 1440)
                {
                    try { await FlushCurrentBarAsync(); }
                    catch (Exception ex) { Console.WriteLine($"CandleService initial flush error: {ex.Message}"); }
                }
                return;
            }

            if (barToEmit != null)
            {
                // Invocar OnUpdate ANTES do broadcast para que o OrchestratorService
                // anexe VolumeLevel/ForecastPrice à barra; do contrário o cliente
                // recebe a barra fechada sem o snapshot de volume ao vivo.
                if (OnUpdate != null)
                    await OnUpdate.Invoke(barToEmit);

                if (hubContext != null)
                {
                    await hubContext.Clients.Group(Symbol).ReceiveOnCloseBar(barToEmit);
                }

                await PersistClosedBarAsync(barToEmit);
            }
        }

        /// <summary>Salva a barra fechada no arquivo da data da barra (não de DateTime.Now).</summary>
        private async Task PersistClosedBarAsync(BarStorageItem barToEmit)
        {
            try
            {
                await EnsureDayRolloverAsync();

                var targetPath = GetPathForDate(barToEmit.Date);

                // Fechamento dentro do mesmo dia: mantém comportamento intraday atual
                // (DataKeep = barras fechadas de hoje).
                if (targetPath == Path)
                {
                    if (DataKeep == null)
                        DataKeep = new List<BarStorageItem>();
                    lock (_barLock)
                    {
                        UpsertBar(DataKeep, barToEmit);
                    }
                    // Copia sob lock para não segurar o lock durante IO.
                    List<BarStorageItem> snapshot;
                    lock (_barLock)
                    {
                        snapshot = DataKeep.Select(CloneBar).ToList();
                    }
                    // SetDataAsync atualiza DataKeep; reatribui a cópia para manter referência consistente.
                    await SetDataAsync(snapshot);
                }
                else
                {
                    // Fechamento cruzando o dia (caso do 1440MIN, fechado no 1º tick do dia seguinte):
                    // grava no arquivo do dia a que a barra pertence, sem contaminar o DataKeep de hoje.
                    var existing = await GetDataAsync(targetPath) ?? new List<BarStorageItem>();
                    UpsertBar(existing, barToEmit);
                    await WriteFileAsync(existing, targetPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CandleService.PersistClosedBar error: {ex.Message}");
            }
        }

        private async Task FlushLoop()
        {
            try
            {
                // Alinha o primeiro flush para não competir com o startup.
                await Task.Delay(TimeSpan.FromSeconds(10));
                while (await _flushTimer.WaitForNextTickAsync())
                {
                    try
                    {
                        await FlushCurrentBarAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"CandleService.FlushLoop error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CandleService.FlushLoop fatal: {ex.Message}");
            }
        }

        /// <summary>Persiste a barra diária aberta (upsert) para que o arquivo de hoje
        /// reflita o pregão em andamento. Não dispara OnUpdate/estrutura.</summary>
        public async Task FlushCurrentBarAsync()
        {
            if (TimeFrame != 1440)
                return;

            BarStorageItem? snapshot;
            lock (_barLock)
            {
                if (_currentBar == null)
                    return;
                snapshot = CloneBar(_currentBar);
            }

            var targetPath = GetPathForDate(snapshot.Date);
            var existing = await GetDataAsync(targetPath) ?? new List<BarStorageItem>();
            UpsertBar(existing, snapshot);
            await WriteFileAsync(existing, targetPath);
        }

        private async Task EnsureDayRolloverAsync()
        {
            var today = DateTime.Now.Date;
            if (today == _loadedDate)
                return;

            try
            {
                await LoadAsync();
                if (DataKeep == null)
                    DataKeep = new List<BarStorageItem>();
                _loadedDate = today;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CandleService.EnsureDayRollover error: {ex.Message}");
            }
        }

        private static void UpsertBar(List<BarStorageItem> list, BarStorageItem bar)
        {
            var idx = list.FindIndex(b => b.Date == bar.Date);
            if (idx >= 0)
                list[idx] = bar;
            else
            {
                list.Add(bar);
                list.Sort((a, b) => a.Date.CompareTo(b.Date));
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
                ForecastPrice = bar.ForecastPrice,
                VolumeLevel = bar.VolumeLevel?.Select(v => new VolumeLevel
                {
                    Price = v.Price,
                    Total = v.Total,
                    BuyVolume = v.BuyVolume,
                    SellVolume = v.SellVolume
                }).ToList(),
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
                TimeFrame = TimeFrame,
                ForecastPrice = null,
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
