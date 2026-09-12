using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;
using Microsoft.AspNetCore.SignalR;

namespace B3WM.Services.Core
{
    /// <summary>
    /// Detecta topos e vales estruturais do perfil de volume de um período.
    /// Entrada: perfil de volume (Price x Total) do dia inteiro ou de um
    /// intervalo selecionado (referência de início/fim pelos snapshots dos
    /// candles, mesmo conceito do Volume Profile).
    ///
    /// Fluxo: VolumeService fornece o perfil cumulativo ao vivo; CandleService
    /// (1MIN) fornece os snapshots para subtrair em períodos parciais. A cada
    /// recomputação o resultado é persistido em JSON e transmitido via SignalR.
    /// </summary>
    public class ExtremeService : DataKeeperService<ExtremeStorageItem>, ISymbolable
    {
        public string Symbol { get; }

        private readonly IHubContext<DataHub, IDataHubClient> _hubContext;
        private readonly ILogger<ExtremeService> _logger;
        private readonly PeriodicTimer _liveTimer = new(TimeSpan.FromSeconds(30));
        private readonly object _lock = new();

        private ExtremeStorageItem _current = new() { Symbol = string.Empty };

        private ExtremeDetectorOptions _config = new();
        public ExtremeDetectorOptions Config => _config;

        public DateTime? PeriodFrom { get; private set; }
        public DateTime? PeriodTo { get; private set; }

        private DateTime _lastLiveCompute = default;

        public override string Path => $"{Symbol}_{nameof(ExtremeService)}_{DateTime.Now:yyyy-MM-dd}.json";

        public ExtremeService(string symbol,
            IHubContext<DataHub, IDataHubClient> hubContext,
            IServiceProvider serviceProvider,
            ILogger<ExtremeService> logger)
            : base(serviceProvider)
        {
            Symbol = symbol;
            _hubContext = hubContext;
            _logger = logger;

            _current.Symbol = symbol;

            // reage ao volume ao vivo (perfil crescente)
            var volumeService = _serviceProvider.GetServices<VolumeService>()
                .FirstOrDefault(v => v.Symbol == Symbol);
            if (volumeService != null)
            {
                volumeService.OnUpdate += OnVolumeUpdate;
            }

            _ = Task.Run(LiveLoop);
        }

        private async Task OnVolumeUpdate(VolumeLevelStorageItem snapshot)
        {
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                if (_lastLiveCompute != default &&
                    (now - _lastLiveCompute).TotalSeconds < 30)
                    return;
                _lastLiveCompute = now;
            }

            try
            {
                await Recompute();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ExtremeService.OnVolumeUpdate error");
            }
        }

        private async Task LiveLoop()
        {
            await LoadAsync();

            lock (_lock)
            {
                if (DataKeep != null && DataKeep.Symbol == Symbol)
                {
                    _current = DataKeep;
                    _config = DataKeep.Config ?? new ExtremeDetectorOptions();
                    PeriodFrom = DataKeep.PeriodFrom;
                    PeriodTo = DataKeep.PeriodTo;
                }
            }

            try
            {
                await Recompute();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ExtremeService initial recompute error");
            }

            while (await _liveTimer.WaitForNextTickAsync())
            {
                try
                {
                    // período que alcança o fim ao vivo → o perfil cresce
                    if (PeriodTo == null)
                    {
                        await Recompute();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ExtremeService.LiveLoop error");
                }
            }
        }

        public ExtremeStorageItem GetSnapshot()
        {
            lock (_lock) return _current;
        }

        /// <summary>Altera os parâmetros de detecção e recomputa.</summary>
        public async Task SetConfig(ExtremeDetectorOptions config, bool recomputeLive = true)
        {
            lock (_lock)
            {
                _config = config ?? new ExtremeDetectorOptions();
            }
            if (recomputeLive)
                await Recompute();
        }

        /// <summary>
        /// Persiste apenas a configuração atual no snapshot, sem recomputar nem transmitir.
        /// Garante que alterações feitas em data histórica sobrevivam a um restart do servidor.
        /// </summary>
        public async Task PersistConfig()
        {
            ExtremeStorageItem snapshot;
            lock (_lock)
            {
                if (_current == null)
                {
                    _current = new ExtremeStorageItem
                    {
                        Date = DateTime.Now,
                        Symbol = Symbol,
                        PeriodFrom = PeriodFrom,
                        PeriodTo = PeriodTo,
                        Config = _config,
                        Extremes = [],
                        Structures = [],
                        Statistics = null
                    };
                }
                else
                {
                    _current.Config = _config;
                }
                snapshot = _current;
            }
            await SetDataAsync(snapshot);
        }

        /// <summary>
        /// Define o período do perfil de volume (null = dia inteiro / fim ao vivo).
        /// </summary>
        public async Task SetPeriod(DateTime? from, DateTime? to)
        {
            lock (_lock)
            {
                PeriodFrom = from;
                PeriodTo = to;
            }
            await Recompute();
        }

        /// <summary>
        /// Recomputa a detecção sobre o perfil do período atual, persiste e transmite.
        /// </summary>
        public async Task Recompute()
        {
            List<VolumeLevel> profile;
            lock (_lock)
            {
                profile = BuildPeriodProfile();
            }

            var prices = profile.Select(v => v.Price).ToArray();
            var totals = profile.Select(v => (double)v.Total).ToArray();

            var result = ExtremeDetector.Detect(prices, totals, _config);

            var snapshot = new ExtremeStorageItem
            {
                Date = DateTime.Now,
                Symbol = Symbol,
                PeriodFrom = PeriodFrom,
                PeriodTo = PeriodTo,
                Config = _config,
                Extremes = result.Extremes,
                Structures = result.Structures,
                Statistics = result.Statistics
            };

            lock (_lock)
            {
                _current = snapshot;
            }

            await SetDataAsync(snapshot);

            if (_hubContext != null)
            {
                await _hubContext.Clients.Group(Symbol).ReceiveOnExtreme(snapshot);
            }
        }

        /// <summary>
        /// Monta o perfil de volume do período selecionado.
        /// Dia inteiro (from/to null) → perfil cumulativo ao vivo do VolumeService.
        /// Parcial → snapshot do fim (ou cumulativo se fim ao vivo) menos snapshot do início.
        /// </summary>
        private List<VolumeLevel> BuildPeriodProfile()
        {
            var volumeService = _serviceProvider.GetServices<VolumeService>()
                .FirstOrDefault(v => v.Symbol == Symbol);
            var cumulative = volumeService?.GetSnapshot()?.Volumes
                .OrderBy(v => v.Price)
                .ToList() ?? new List<VolumeLevel>();

            if (PeriodFrom == null && PeriodTo == null)
                return cumulative;

            var candles = _serviceProvider.GetServices<CandleService>()
                .FirstOrDefault(c => c.Symbol == Symbol && c.TimeFrame == 1)
                ?.DataKeep?
                .OrderBy(b => b.Date)
                .ToList();

            List<VolumeLevel> toLevels = cumulative;
            if (PeriodTo != null && candles != null && candles.Count > 0)
            {
                var toBar = candles.LastOrDefault(b => b.Date <= PeriodTo.Value);
                if (toBar?.VolumeLevel != null && toBar.VolumeLevel.Count > 0)
                    toLevels = toBar.VolumeLevel;
            }

            List<VolumeLevel> fromLevels = new();
            if (PeriodFrom != null && candles != null && candles.Count > 0)
            {
                var fromBar = candles.LastOrDefault(b => b.Date <= PeriodFrom.Value);
                if (fromBar?.VolumeLevel != null)
                    fromLevels = fromBar.VolumeLevel;
            }

            if (fromLevels.Count == 0)
                return toLevels;

            var fromByPrice = fromLevels.ToDictionary(v => v.Price, v => v.Total);
            var result = new List<VolumeLevel>();
            foreach (var to in toLevels)
            {
                double diff = to.Total - (fromByPrice.TryGetValue(to.Price, out var f) ? f : 0);
                if (diff <= 0) continue;
                result.Add(new VolumeLevel
                {
                    Price = to.Price,
                    Total = (long)diff,
                    BuyVolume = to.BuyVolume,
                    SellVolume = to.SellVolume
                });
            }

            return result.OrderBy(v => v.Price).ToList();
        }

        /// <summary>
        /// Computa a detecção para uma data arbitrária (histórica), lendo o
        /// perfil de volume persistido daquele dia. Função pura: não altera o
        /// estado ao vivo (período, snapshot, timers, broadcasts).
        /// </summary>
        public async Task<ExtremeStorageItem> ComputeForDate(DataKeeperBase keeper, DateTime date, DateTime? from, DateTime? to)
        {
            var profile = await BuildProfileFromFiles(keeper, Symbol, date, from, to);

            var prices = profile.Select(v => v.Price).ToArray();
            var totals = profile.Select(v => (double)v.Total).ToArray();

            ExtremeDetectorOptions config;
            lock (_lock)
            {
                config = _config;
            }

            var result = ExtremeDetector.Detect(prices, totals, config);

            _logger.LogInformation(
                "ExtremeService.ComputeForDate {Symbol} {Date:yyyy-MM-dd} from={From} to={To} profile={Profile} extremes={Extremes}",
                Symbol, date.Date, from, to, profile.Count, result.Extremes.Count);

            return new ExtremeStorageItem
            {
                Date = date.Date,
                Symbol = Symbol,
                PeriodFrom = from,
                PeriodTo = to,
                Config = config,
                Extremes = result.Extremes,
                Structures = result.Structures,
                Statistics = result.Statistics
            };
        }

        /// <summary>
        /// Monta o perfil de volume de um dia a partir dos arquivos persistidos
        /// (mesmo conceito do Volume Profile do cliente, que lê o arquivo da data).
        /// Dia inteiro → arquivo do VolumeService da data.
        /// Parcial → snapshot do fim (ou dia inteiro se fim nulo) menos snapshot
        /// do início, usando os snapshots dos candles 1MIN persistidos da data.
        /// </summary>
        public static async Task<List<VolumeLevel>> BuildProfileFromFiles(DataKeeperBase keeper, string symbol, DateTime date, DateTime? from, DateTime? to)
        {
            var dayPath = $"{symbol}_{nameof(VolumeService)}_{date:yyyy-MM-dd}.json";
            var day = await keeper.ReadDataAsync<VolumeLevelStorageItem>(dayPath);
            var dayLevels = day?.Volumes?.OrderBy(v => v.Price).ToList() ?? new List<VolumeLevel>();

            if (from == null && to == null)
                return dayLevels;

            var candlePath = $"{symbol}_{nameof(CandleService)}_1MIN_{date:yyyy-MM-dd}.json";
            var candles = (await keeper.ReadDataAsync<List<BarStorageItem>>(candlePath))
                ?.OrderBy(b => b.Date)
                .ToList() ?? new List<BarStorageItem>();

            List<VolumeLevel> toLevels = dayLevels;
            if (to != null && candles.Count > 0)
            {
                var toBar = candles.LastOrDefault(b => b.Date <= to.Value);
                if (toBar?.VolumeLevel != null && toBar.VolumeLevel.Count > 0)
                    toLevels = toBar.VolumeLevel;
            }

            List<VolumeLevel> fromLevels = new();
            if (from != null && candles.Count > 0)
            {
                var fromBar = candles.LastOrDefault(b => b.Date <= from.Value);
                if (fromBar?.VolumeLevel != null)
                    fromLevels = fromBar.VolumeLevel;
            }

            if (fromLevels.Count == 0)
                return toLevels;

            var fromByPrice = fromLevels.ToDictionary(v => v.Price, v => v.Total);
            var result = new List<VolumeLevel>();
            foreach (var lvl in toLevels)
            {
                double diff = lvl.Total - (fromByPrice.TryGetValue(lvl.Price, out var f) ? f : 0);
                if (diff <= 0) continue;
                result.Add(new VolumeLevel
                {
                    Price = lvl.Price,
                    Total = (long)diff,
                    BuyVolume = lvl.BuyVolume,
                    SellVolume = lvl.SellVolume
                });
            }

            return result.OrderBy(v => v.Price).ToList();
        }

        /// <summary>
        /// Computa a detecção sobre o perfil de volume agregado multi-dia
        /// (referência diária estática para overlay no intraday). Função pura:
        /// soma os arquivos diários do VolumeService na janela [from, to]
        /// (dia-granularidade) e roda o mesmo ExtremeDetector. Não altera o
        /// estado ao vivo (período, snapshot, timers, broadcasts).
        /// </summary>
        public async Task<ExtremeStorageItem> ComputeDailyRange(
            DataKeeperBase keeper, DateTime from, DateTime to, ExtremeDetectorOptions? options = null)
        {
            var fromDate = from.Date;
            var toDate = to.Date;
            if (toDate < fromDate)
                (fromDate, toDate) = (toDate, fromDate);

            // Trava de segurança: evita loop gigante por erro de parâmetro.
            if ((toDate - fromDate).TotalDays > 365)
                fromDate = toDate.AddDays(-365);

            var profile = await BuildDailyProfileFromFiles(keeper, Symbol, fromDate, toDate);

            var prices = profile.Select(v => v.Price).ToArray();
            var totals = profile.Select(v => (double)v.Total).ToArray();

            ExtremeDetectorOptions config;
            if (options != null)
            {
                config = options.Clone();
            }
            else
            {
                lock (_lock)
                {
                    config = _config.Clone();
                }
            }

            var result = ExtremeDetector.Detect(prices, totals, config);

            _logger.LogInformation(
                "ExtremeService.ComputeDailyRange {Symbol} from={From:yyyy-MM-dd} to={To:yyyy-MM-dd} profile={Profile} extremes={Extremes}",
                Symbol, fromDate, toDate, profile.Count, result.Extremes.Count);

            return new ExtremeStorageItem
            {
                Date = toDate,
                Symbol = Symbol,
                PeriodFrom = fromDate,
                PeriodTo = toDate,
                Config = config,
                Extremes = result.Extremes,
                Structures = result.Structures,
                Statistics = result.Statistics
            };
        }

        /// <summary>
        /// Monta o perfil de volume agregado multi-dia somando os arquivos
        /// diários do VolumeService (cumulativo tick-a-tick real de cada dia).
        /// Dias ausentes (fds/feriado) são ignorados. É o equivalente diário
        /// do Volume Profile: preço x total de contratos na janela.
        /// </summary>
        public static async Task<List<VolumeLevel>> BuildDailyProfileFromFiles(
            DataKeeperBase keeper, string symbol, DateTime fromDate, DateTime toDate)
        {
            var from = fromDate.Date;
            var to = toDate.Date;
            if (to < from)
                (from, to) = (to, from);
            if ((to - from).TotalDays > 365)
                from = to.AddDays(-365);

            var byPrice = new Dictionary<double, (long Total, long Buy, long Sell)>();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var dayPath = $"{symbol}_{nameof(VolumeService)}_{day:yyyy-MM-dd}.json";
                VolumeLevelStorageItem dayItem;
                try
                {
                    dayItem = await keeper.ReadDataAsync<VolumeLevelStorageItem>(dayPath);
                }
                catch
                {
                    continue;
                }
                if (dayItem?.Volumes == null || dayItem.Volumes.Count == 0)
                    continue;
                foreach (var lvl in dayItem.Volumes)
                {
                    if (byPrice.TryGetValue(lvl.Price, out var acc))
                    {
                        byPrice[lvl.Price] = (
                            acc.Total + lvl.Total,
                            acc.Buy + lvl.BuyVolume,
                            acc.Sell + lvl.SellVolume);
                    }
                    else
                    {
                        byPrice[lvl.Price] = (lvl.Total, lvl.BuyVolume, lvl.SellVolume);
                    }
                }
            }

            return byPrice
                .Where(kv => kv.Value.Total > 0)
                .Select(kv => new VolumeLevel
                {
                    Price = kv.Key,
                    Total = kv.Value.Total,
                    BuyVolume = kv.Value.Buy,
                    SellVolume = kv.Value.Sell
                })
                .OrderBy(v => v.Price)
                .ToList();
        }

        public void Dispose()
        {
            _liveTimer.Dispose();
            var volumeService = _serviceProvider.GetServices<VolumeService>()
                .FirstOrDefault(v => v.Symbol == Symbol);
            if (volumeService != null)
            {
                volumeService.OnUpdate -= OnVolumeUpdate;
            }
        }
    }
}