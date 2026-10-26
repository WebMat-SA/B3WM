using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services.Core
{
    /// <summary>
    /// Fonte única da VWAP: typical HLC/3 acumulado a partir dos candles 1MIN fechados.
    /// Disparado pelo OrchestratorService no fechamento do 1MIN (não consome ticks direto).
    /// Persiste todo minuto (inclui repetição do último valor em minutos sem negócio)
    /// em B3WM/Data/{SYM}_VwapService_yyyy-MM-dd.json como lista do dia.
    /// </summary>
    public class VwapService : DataKeeperService<List<VwapHistoryPoint>>, ISymbolable
    {
        public string Symbol { get; }

        private readonly IHubContext<DataHub, IDataHubClient> hubContext;
        private readonly ILogger<VwapService> _logger;

        private readonly object _lock = new();
        private double _runningSumPv;
        private long _runningSumV;
        private DateTime _currentDay;
        private List<VwapHistoryPoint> _history = new();

        public event Func<VwapHistoryPoint, Task>? OnUpdate;

        public override string Path => GetPathForDate(DateTime.Now);

        public string GetPathForDate(DateTime date) => $"{Symbol}_{nameof(VwapService)}_{date:yyyy-MM-dd}.json";

        public VwapService(string symbol, IHubContext<DataHub, IDataHubClient> hubContext, IServiceProvider serviceProvider, ILogger<VwapService> logger)
            : base(serviceProvider)
        {
            Symbol = symbol;
            this.hubContext = hubContext;
            _logger = logger;

            _ = Task.Run(InitAsync);
        }

        public static double TypicalPrice(BarStorageItem bar) => (bar.High + bar.Low + bar.Close) / 3.0;

        public VwapHistoryPoint? GetSnapshot()
        {
            lock (_lock)
            {
                return _history.Count > 0 ? Clone(_history[^1]) : null;
            }
        }

        public List<VwapHistoryPoint> GetHistory()
        {
            lock (_lock)
            {
                return _history.Select(Clone).ToList();
            }
        }

        private async Task InitAsync()
        {
            try
            {
                await LoadTodayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VwapService.Init error {Symbol}", Symbol);
            }
        }

        private async Task LoadTodayAsync()
        {
            var today = DateTime.Now.Date;
            List<VwapHistoryPoint> loaded;
            try
            {
                loaded = await GetDataAsync(GetPathForDate(today)) ?? new();
            }
            catch
            {
                loaded = new();
            }

            if (loaded.Count > 0)
            {
                lock (_lock)
                {
                    _history = loaded.OrderBy(p => p.Minute).ToList();
                    var last = _history[^1];
                    _runningSumPv = last.SumPv;
                    _runningSumV = last.SumV;
                    _currentDay = last.Day != default ? last.Day.Date : last.Minute.Date;
                    DataKeep = _history.Select(Clone).ToList();
                }
                _logger.LogInformation("VwapService loaded {Count} points for {Symbol} {Day}", loaded.Count, Symbol, today.ToString("yyyy-MM-dd"));
                return;
            }

            // Sem histórico de hoje: reconstrói a partir dos candles 1MIN já persistidos.
            await RebuildFromCandlesAsync(today);
        }

        private async Task<U> ReadFileAsync<U>(string path) where U : new()
        {
            using var scope = _serviceProvider.CreateScope();
            var keeper = scope.ServiceProvider.GetService<DataKeeperBase>();
            if (keeper == null) return new U();
            return await keeper.ReadDataAsync<U>(path);
        }

        /// <summary>
        /// Cálculo puro do histórico VWAP do dia a partir dos candles 1MIN
        /// fechados (typical HLC/3 acumulado, com preenchimento de minutos
        /// vazios repetindo o último VWAP). Mesma matemática do
        /// <see cref="AppendClosedBarAsync"/>, mas sem tocar no estado vivo
        /// do singleton — seguro para backfill de dias anteriores (issue:
        /// VWAP ausente nos dias anteriores por falta do arquivo do dia).
        /// </summary>
        public static List<VwapHistoryPoint> BuildHistory(
            string symbol, IEnumerable<BarStorageItem> bars, DateTime day)
        {
            var dayBars = bars
                .Where(b => b != null && b.Symbol == symbol && b.TimeFrame == 1 && b.Date.Date == day.Date)
                .OrderBy(b => b.Date)
                .ToList();

            var history = new List<VwapHistoryPoint>();
            var seen = new HashSet<DateTime>();
            double sumPv = 0;
            long sumV = 0;

            foreach (var bar in dayBars)
            {
                // Primeira ocorrência do minuto vence (igual ao guard de
                // duplicata/reprocessamento do AppendClosedBarAsync).
                if (!seen.Add(bar.Date)) continue;

                // Gap de minutos sem barra: repete último VWAP.
                if (history.Count > 0)
                {
                    var lastMinute = history[^1].Minute;
                    var gap = bar.Date - lastMinute;
                    if (gap.TotalMinutes > 1)
                    {
                        var lastVwap = history[^1].Vwap;
                        var cursor = lastMinute.AddMinutes(1);
                        while (cursor < bar.Date)
                        {
                            history.Add(new VwapHistoryPoint
                            {
                                Minute = cursor,
                                Vwap = lastVwap,
                                SumPv = sumPv,
                                SumV = sumV,
                                Symbol = symbol,
                                Day = day.Date
                            });
                            cursor = cursor.AddMinutes(1);
                        }
                    }
                }

                if (bar.Volume > 0)
                {
                    sumPv += TypicalPrice(bar) * bar.Volume;
                    sumV += bar.Volume;
                }

                history.Add(new VwapHistoryPoint
                {
                    Minute = bar.Date,
                    Vwap = sumV > 0 ? sumPv / sumV : 0,
                    SumPv = sumPv,
                    SumV = sumV,
                    Symbol = symbol,
                    Day = day.Date
                });
            }

            return history;
        }

        /// <summary>Reconstrói o acumulado do dia a partir do arquivo CandleService 1MIN.</summary>
        public async Task RebuildFromCandlesAsync(DateTime day)
        {
            try
            {
                var candlePath = $"{Symbol}_{nameof(CandleService)}_1MIN_{day:yyyy-MM-dd}.json";
                var bars = await ReadFileAsync<List<BarStorageItem>>(candlePath) ?? new();
                var history = BuildHistory(Symbol, bars, day.Date);

                lock (_lock)
                {
                    _history = history;
                    if (history.Count > 0)
                    {
                        var last = history[^1];
                        _runningSumPv = last.SumPv;
                        _runningSumV = last.SumV;
                    }
                    else
                    {
                        _runningSumPv = 0;
                        _runningSumV = 0;
                    }
                    _currentDay = day.Date;
                }

                if (history.Count > 0)
                {
                    await WriteFileAsync(history, GetPathForDate(day));
                    DataKeep = history.Select(Clone).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VwapService.RebuildFromCandles error {Symbol} {Day}", Symbol, day.ToString("yyyy-MM-dd"));
            }
        }

        private async Task EnsureDayAsync(DateTime barDay)
        {
            DateTime current;
            lock (_lock) current = _currentDay;
            if (current == barDay) return;

            // Troca de dia: persiste o dia anterior (já está salvo por minuto) e zera.
            lock (_lock)
            {
                _history = new();
                _runningSumPv = 0;
                _runningSumV = 0;
                _currentDay = barDay;
                DataKeep = new List<VwapHistoryPoint>();
            }

            // Tenta carregar arquivo do novo dia (caso restart com dados parciais).
            try
            {
                var existing = await GetDataAsync(GetPathForDate(barDay)) ?? new();
                if (existing.Count > 0)
                {
                    lock (_lock)
                    {
                        _history = existing.OrderBy(p => p.Minute).ToList();
                        var last = _history[^1];
                        _runningSumPv = last.SumPv;
                        _runningSumV = last.SumV;
                        DataKeep = _history.Select(Clone).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VwapService.EnsureDay load error {Symbol}", Symbol);
            }
        }

        /// <summary>
        /// Chamado pelo OrchestratorService a cada fechamento de candle 1MIN.
        /// Preenche minutos vazios repetindo o último VWAP (histórico corrido).
        /// </summary>
        public async Task<VwapHistoryPoint?> AppendClosedBarAsync(BarStorageItem bar, bool persistPerBar = true)
        {
            if (bar == null || bar.Symbol != Symbol) return null;
            if (bar.TimeFrame != 1) return null;

            var barDay = bar.Date.Date;
            if (_currentDay == default)
            {
                lock (_lock) _currentDay = barDay;
                await LoadTodayAsync();
                // LoadToday pode ter reconstruído incluindo esta barra; evita duplicar.
                lock (_lock)
                {
                    if (_history.Any(p => p.Minute == bar.Date))
                        return Clone(_history.First(p => p.Minute == bar.Date));
                }
            }
            else if (barDay != _currentDay)
            {
                await EnsureDayAsync(barDay);
            }

            VwapHistoryPoint point;
            List<VwapHistoryPoint> toBroadcast = new();
            lock (_lock)
            {
                // Preenche gap de minutos sem barra: repete último VWAP.
                if (_history.Count > 0)
                {
                    var lastMinute = _history[^1].Minute;
                    var gap = bar.Date - lastMinute;
                    if (gap.TotalMinutes > 1)
                    {
                        var lastVwap = _history[^1].Vwap;
                        var cursor = lastMinute.AddMinutes(1);
                        while (cursor < bar.Date)
                        {
                            var filler = new VwapHistoryPoint
                            {
                                Minute = cursor,
                                Vwap = lastVwap,
                                SumPv = _runningSumPv,
                                SumV = _runningSumV,
                                Symbol = Symbol,
                                Day = _currentDay
                            };
                            _history.Add(filler);
                            toBroadcast.Add(Clone(filler));
                            cursor = cursor.AddMinutes(1);
                        }
                    }
                    else if (gap.TotalMinutes < 1 && _history.Any(p => p.Minute == bar.Date))
                    {
                        // Duplicata/reprocessamento: retorna existente sem acumular de novo.
                        return Clone(_history.First(p => p.Minute == bar.Date));
                    }
                }

                var typical = TypicalPrice(bar);
                if (bar.Volume > 0)
                {
                    _runningSumPv += typical * bar.Volume;
                    _runningSumV += bar.Volume;
                }

                double vwap = 0;
                if (_runningSumV > 0)
                    vwap = _runningSumPv / _runningSumV;

                point = new VwapHistoryPoint
                {
                    Minute = bar.Date,
                    Vwap = vwap,
                    SumPv = _runningSumPv,
                    SumV = _runningSumV,
                    Symbol = Symbol,
                    Day = _currentDay == default ? barDay : _currentDay
                };

                // Upsert por minuto.
                var idx = _history.FindIndex(p => p.Minute == point.Minute);
                if (idx >= 0) _history[idx] = point;
                else _history.Add(point);
                _history.Sort((a, b) => a.Minute.CompareTo(b.Minute));
                toBroadcast.Add(Clone(point));

                DataKeep = _history.Select(Clone).ToList();
            }

            if (persistPerBar && point.SumV > 0)
            {
                try
                {
                    List<VwapHistoryPoint> snapshot;
                    lock (_lock) snapshot = _history.Select(Clone).ToList();
                    await WriteFileAsync(snapshot, GetPathForDate(point.Day));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VwapService persist error {Symbol}", Symbol);
                }
            }

            // Broadcast: novo canal + compat com ReceiveOnForecast.
            try
            {
                if (hubContext != null)
                {
                    await hubContext.Clients.Group(Symbol).ReceiveVwapUpdate(Clone(point));
#pragma warning disable CS0618 // Compat: mantém clientes antigos que ouvem forecast.
                    await hubContext.Clients.Group(Symbol).ReceiveOnForecast(new AdjustmentForecastItem
                    {
                        Vwap = point.Vwap,
                        Time = point.Minute,
                        Symbol = point.Symbol,
                        SumPv = point.SumPv,
                        SumV = point.SumV,
                        Day = point.Day
                    });
#pragma warning restore CS0618
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VwapService broadcast error {Symbol}", Symbol);
            }

            if (OnUpdate != null)
            {
                try { await OnUpdate.Invoke(Clone(point)); }
                catch (Exception ex) { _logger.LogWarning(ex, "VwapService.OnUpdate error"); }
            }

            // Emite fillers que foram criados no gap (para clientes em tempo real).
            if (toBroadcast.Count > 1 && hubContext != null)
            {
                try
                {
                    await hubContext.Clients.Group(Symbol).ReceiveVwapHistory(GetHistory());
                }
                catch { /* throttling já cobre o snapshot */ }
            }

            return Clone(point);
        }

        private static VwapHistoryPoint Clone(VwapHistoryPoint p) => new()
        {
            Minute = p.Minute,
            Vwap = p.Vwap,
            SumPv = p.SumPv,
            SumV = p.SumV,
            Symbol = p.Symbol,
            Day = p.Day
        };
    }
}
