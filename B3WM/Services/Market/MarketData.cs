using B3WM.Services.Core;

using B3WM.Shared.Entity;

using B3WM.Shared.Models;

using B3WM.Shared.Models.ExtremeDetection;



namespace B3WM.Services.Market

{

    public sealed class MarketData : IMarketData

    {

        private readonly DataKeeperBase _keeper;

        private readonly IEnumerable<CandleService> _candles;

        private readonly IEnumerable<BubbleService> _bubbles;

        private readonly IEnumerable<ExtremeService> _extremes;

        private readonly IEnumerable<StructureService> _structures;

        private readonly ILogger<MarketData> _logger;



        public MarketData(

            DataKeeperBase keeper,

            IEnumerable<CandleService> candles,

            IEnumerable<BubbleService> bubbles,

            IEnumerable<ExtremeService> extremes,

            IEnumerable<StructureService> structures,

            ILogger<MarketData> logger)

        {

            _keeper = keeper;

            _candles = candles;

            _bubbles = bubbles;

            _extremes = extremes;

            _structures = structures;

            _logger = logger;

        }



        private static bool IsToday(DateTime? date) =>

            date == null || date.Value.Date == DateTime.Today;



        public Task<List<BarStorageItem>> CandlesAsync(

            string symbol, int timeFrame, DateTime? date = null, int takeLast = int.MaxValue, CancellationToken ct = default)

        {

            var tf = Defaults.TimeFrames.Contains(timeFrame) ? timeFrame : 5;

            if (IsToday(date))

            {

                var svc = _candles.FirstOrDefault(c => c.Symbol == symbol && c.TimeFrame == tf)

                    ?? _candles.FirstOrDefault(c => c.Symbol == symbol);

                var live = svc?.DataKeep?.OrderBy(b => b.Date).ToList() ?? new();

                if (live.Count > 0)

                    return Task.FromResult(takeLast == int.MaxValue ? live : live.TakeLast(takeLast).ToList());

            }

            return ReadCandlesFileAsync(symbol, tf, (date ?? DateTime.Today).Date, takeLast);

        }



        public async Task<List<BarStorageItem>> ReadCandlesFileAsync(string symbol, int tf, DateTime date, int take)

        {

            var path = $"{symbol}_{nameof(CandleService)}_{tf}MIN_{date:yyyy-MM-dd}.json";

            var day = await _keeper.ReadDataAsync<List<BarStorageItem>>(path);

            var ordered = (day ?? new()).OrderBy(b => b.Date).ToList();

            return take == int.MaxValue ? ordered : ordered.TakeLast(take).ToList();

        }



        public Task<List<BubbleStorageItem>> BubblesAsync(string symbol, DateTime? date = null, CancellationToken ct = default)

        {

            if (IsToday(date))

            {

                var live = _bubbles.FirstOrDefault(b => b.Symbol == symbol)

                    ?.DataKeep?.OrderBy(b => b.Date).ToList();

                if (live is { Count: > 0 })

                    return Task.FromResult(live);

            }

            return ReadBubblesFileAsync(symbol, (date ?? DateTime.Today).Date);

        }



        public async Task<List<BubbleStorageItem>> ReadBubblesFileAsync(string symbol, DateTime date)

        {

            var path = $"{symbol}_{nameof(BubbleService)}_{date:yyyy-MM-dd}.json";

            var day = await _keeper.ReadDataAsync<List<BubbleStorageItem>>(path);

            return (day ?? new()).OrderBy(b => b.Date).ToList();

        }



        public async Task<VolumeLevelStorageItem?> VolumeAsync(string symbol, DateTime? date = null, CancellationToken ct = default)

        {

            // Arquivo do dia: ao vivo está naturalmente parcial em T;

            // histórico retorna o dia fechado.

            var path = $"{symbol}_{nameof(VolumeService)}_{(date ?? DateTime.Today):yyyy-MM-dd}.json";

            return await _keeper.ReadDataAsync<VolumeLevelStorageItem>(path);

        }



        public async Task<List<VolumeLevel>> VolumeLevelsAsync(

            string symbol, DateTime date, DateTime? from, DateTime? to, CancellationToken ct = default)

        {

            if (from == null && to == null)

            {

                var day = await VolumeAsync(symbol, date, ct);

                return day?.Volumes.OrderBy(v => v.Price).ToList() ?? new();

            }

            return await ExtremeService.BuildProfileFromFiles(_keeper, symbol, date, from, to);

        }



        public Task<List<StructureStorageItem>> StructuresAsync(

            string symbol, int timeFrame, double minDistance, DateTime? date = null, CancellationToken ct = default)

        {

            if (IsToday(date))

            {

                var live = _structures

                    .FirstOrDefault(s => s.Symbol == symbol && s.TimeFrame == timeFrame)

                    ?.DataKeep?.ToList();

                if (live is { Count: > 0 })

                    return Task.FromResult(live.OrderBy(s => s.Date).ToList());

            }

            return ReadStructuresFileAsync(symbol, timeFrame, minDistance,

                (date ?? DateTime.Today).Date,

                _structures.FirstOrDefault(s => s.Symbol == symbol && s.TimeFrame == timeFrame)

                    ?._minDistanceUpdateBorder ?? minDistance);

        }



        private async Task<List<StructureStorageItem>> ReadStructuresFileAsync(

            string symbol, int tf, double minDistance, DateTime date, double serviceDist)

        {

            foreach (var d in new[] { minDistance, serviceDist }.Distinct().ToList())

            {

                try

                {

                    var path = $"{symbol}_{nameof(StructureService)}_{tf}MIN_{d}_{date:yyyy-MM-dd}.json";

                    var day = await _keeper.ReadDataAsync<List<StructureStorageItem>>(path);

                    if (day is { Count: > 0 })

                        return day.OrderBy(s => s.Date).ToList();

                }

                catch (Exception ex) { _logger.LogWarning(ex, "Falha lendo structures {Symbol} {Tf}MIN (I/O; dia ausente não lança)", symbol, tf); }

            }

            return new();

        }



        public async Task<List<StructureStorageItem>> DailyStructuresAsync(

            string symbol, double dailyDistance, DateTime date, int days = 90, CancellationToken ct = default)

        {

            days = Math.Clamp(days, 2, 365);

            var all = new List<StructureStorageItem>();

            for (var d = 0; d < days; d++)

            {

                var day = date.AddDays(-d);

                var path = $"{symbol}_{nameof(StructureService)}_1440MIN_{dailyDistance}_{day:yyyy-MM-dd}.json";

                try

                {

                    var items = await _keeper.ReadDataAsync<List<StructureStorageItem>>(path);

                    if (items != null) all.AddRange(items);

                }

                catch (Exception ex) { _logger.LogWarning(ex, "Falha lendo structures 1440 {Symbol} (I/O; dia ausente não lança)", symbol); }

            }

            return all.OrderBy(s => s.Date).ToList();

        }



        public async Task<ExtremeStorageItem?> IntradayExtremesAsync(string symbol, CancellationToken ct = default)

        {

            var svc = _extremes.FirstOrDefault(s => s.Symbol == symbol);

            if (svc?.GetSnapshot() is { } snap && snap.Extremes.Count > 0)

                return Clone(snap);

            try

            {

                var file = await _keeper.ReadDataAsync<ExtremeStorageItem>(

                    $"{symbol}_{nameof(ExtremeService)}_{DateTime.Today:yyyy-MM-dd}.json");

                return file?.Extremes.Count > 0 ? file : null;

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Falha lendo extremes do dia {Symbol}", symbol);

                return null;

            }

        }



        public async Task<ExtremeStorageItem?> DailyExtremesAsync(

            string symbol, DateTime from, DateTime to, double noise, double prom, CancellationToken ct = default)

        {

            try

            {

                var svc = _extremes.FirstOrDefault(s => s.Symbol == symbol);

                if (svc == null) return null;

                var item = await svc.ComputeDailyRange(_keeper, from.Date, to.Date,

                    new ExtremeDetectorOptions { NoiseSensitivity = noise, MinimumProminence = prom });

                return item.Extremes.Count > 0 ? item : null;

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Falha ao computar extremos diários {Symbol}", symbol);

                return null;

            }

        }



        public async Task<PivotStorageItem?> IntradayPivotAsync(string symbol, int lineCount, DateTime? date = null, CancellationToken ct = default)

        {

            var target = (date ?? DateTime.Today).Date;

            try

            {

                var bars = await PivotService.ReadDailyBarsAsync(_keeper, symbol, target.AddDays(-60), target);

                var session = PivotService.ResolveSessionDate(bars, target);

                if (session == null) return null;

                var src = PivotService.ResolveIntradaySource(bars, session.Value);

                if (src == null) return null;

                return new PivotStorageItem

                {

                    Symbol = symbol, Date = session.Value, Source = "D-1",

                    High = src.High, Low = src.Low, Close = src.Close, LineCount = lineCount,

                    Levels = PivotService.ComputeTraditional(src.High, src.Low, src.Close, lineCount),

                };

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Falha no pivot intraday {Symbol}", symbol);

                return null;

            }

        }



        public async Task<PivotStorageItem?> DailyPivotAsync(string symbol, int lineCount, DateTime? date = null, CancellationToken ct = default)

        {

            var target = (date ?? DateTime.Today).Date;

            try

            {

                var bars = await PivotService.ReadDailyBarsAsync(_keeper, symbol, target.AddDays(-60), target);

                var session = PivotService.ResolveSessionDate(bars, target);

                if (session == null) return null;

                var weekly = PivotService.ResolveDailySource(bars, session.Value);

                double h, l, c;

                string source;

                if (weekly != null) { (h, l, c) = weekly.Value; source = "W-1"; }

                else

                {

                    var src = PivotService.ResolveIntradaySource(bars, session.Value);

                    if (src == null) return null;

                    h = src.High; l = src.Low; c = src.Close; source = "D-1";

                }

                return new PivotStorageItem

                {

                    Symbol = symbol, Date = session.Value, Source = source,

                    High = h, Low = l, Close = c, LineCount = lineCount,

                    Levels = PivotService.ComputeTraditional(h, l, c, lineCount),

                };

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Falha no pivot diário {Symbol}", symbol);

                return null;

            }

        }



        private static ExtremeStorageItem? Clone(ExtremeStorageItem snap) => new()

        {

            Id = snap.Id, Date = snap.Date, Symbol = snap.Symbol,

            PeriodFrom = snap.PeriodFrom, PeriodTo = snap.PeriodTo, Config = snap.Config,

            Extremes = snap.Extremes.ToList(), Structures = snap.Structures.ToList(),

            Statistics = snap.Statistics,

        };

    }

}

