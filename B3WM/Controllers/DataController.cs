using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;
using Microsoft.AspNetCore.Mvc;
namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class DataController : ControllerBase
    {
        private readonly DataKeeperBase dataKeeper;
        private readonly IEnumerable<StructureService> structureServices;
        private readonly IEnumerable<CandleService> _candleServices;
        private readonly IEnumerable<BubbleService> _bubbleServices;
        private readonly IEnumerable<ExtremeService> _extremeServices;
        private readonly ILogger<DataController> _logger;

        public DataController(DataKeeperBase dataKeeper, IEnumerable<StructureService> structureServices, IEnumerable<CandleService> candleServices, IEnumerable<BubbleService> bubbleServices, IEnumerable<ExtremeService> extremeServices, ILogger<DataController> logger)
        {
            this.dataKeeper = dataKeeper;
            this.structureServices = structureServices;
            _candleServices = candleServices;
            _bubbleServices = bubbleServices;
            _extremeServices = extremeServices;
            _logger = logger;
        }

        [HttpGet("{symbol}/{date}")]
        public async Task<IActionResult> GetBarAsync(string symbol, DateTime date)
        {
            var tasks = Defaults.TimeFrames.Select(timeFrame =>
            {
                string path = $"{symbol}_{nameof(CandleService)}_{timeFrame}MIN_{date:yyyy-MM-dd}.json";
                return dataKeeper.ReadDataAsync<List<BarStorageItem>>(path);
            });

            var results = await Task.WhenAll(tasks);
            var data = results.SelectMany(r => r).ToList();

            return Ok(data);
        }

        [HttpGet("{symbol}/{startDate}/{endDate}/{timeFrame}")]
        public async Task<IActionResult> GetBarRange(string symbol, DateTime startDate, DateTime endDate, int timeFrame)
        {
            var allBars = new List<BarStorageItem>();
            var current = startDate.Date;

            while (current <= endDate.Date)
            {
                var path = $"{symbol}_{nameof(CandleService)}_{timeFrame}MIN_{current:yyyy-MM-dd}.json";
                try
                {
                    var dayBars = await dataKeeper.ReadDataAsync<List<BarStorageItem>>(path);
                    allBars.AddRange(dayBars);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load bars for {Date}", current.ToString("yyyy-MM-dd"));
                }
                current = current.AddDays(1);
            }

            var filtered = allBars
                .Where(b => b.Date >= startDate && b.Date <= endDate)
                .OrderBy(b => b.Date)
                .ToList();

            return Ok(filtered);
        }

        [HttpGet("{symbol}/{startDate}/{endDate}")]
        public async Task<IActionResult> GetBubbleRange(string symbol, DateTime startDate, DateTime endDate)
        {
            var allBubbles = new List<BubbleStorageItem>();
            var current = startDate.Date;

            while (current <= endDate.Date)
            {
                var path = $"{symbol}_{nameof(BubbleService)}_{current:yyyy-MM-dd}.json";
                try
                {
                    var dayBubbles = await dataKeeper.ReadDataAsync<List<BubbleStorageItem>>(path);
                    if (dayBubbles != null)
                        allBubbles.AddRange(dayBubbles);
                }
                catch
                {
                    // skip missing days
                }
                current = current.AddDays(1);
            }

            var filtered = allBubbles
                .Where(b => b.Date >= startDate && b.Date <= endDate)
                .ToList();

            return Ok(filtered);
        }

        [HttpGet("{symbol}/{date}")]
        public async Task<IActionResult> GetBubbleAsync(string symbol, DateTime date)
        {
            string path = $"{symbol}_{nameof(BubbleService)}_{date:yyyy-MM-dd}.json";

            var data = await dataKeeper.ReadDataAsync<List<BubbleStorageItem>>(path);

            return Ok(data);
        }

        [HttpGet("{symbol}/{date}/{minDistance:double}")]
        public async Task<IActionResult> GetStructureAsync(string symbol, DateTime date, double minDistance)
        {

            List<StructureStorageItem> data = new List<StructureStorageItem>();

            foreach (var timeFrame in Defaults.TimeFrames)
            {
                // O 1440 (1D) tem distância própria (seção diária, issue #10):
                // usa a distância atual do serviço e nunca é resetado pelo
                // parâmetro intraday.
                var service = structureServices.FirstOrDefault(s => s.Symbol == symbol && s.TimeFrame == timeFrame);
                var dist = timeFrame == 1440
                    ? service?._minDistanceUpdateBorder ?? minDistance
                    : minDistance;
                string path = $"{symbol}_{nameof(StructureService)}_{timeFrame}MIN_{dist}_{date:yyyy-MM-dd}.json";

                var timeframeData = await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);

                //hardening: se o arquivo ainda estiver vazio (ex: servidor iniciou agora e o
                //backfill ainda nao rodou), forca o PreLoad do service e relê
                if (timeframeData == null || timeframeData.Count == 0)
                {
                    if (service != null)
                    {
                        if (timeFrame != 1440 && service._minDistanceUpdateBorder != minDistance)
                            await service.SetMinDistance(minDistance);
                        else
                            await service.PreLoad();

                        timeframeData = await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);
                    }
                }

                if (timeframeData != null)
                    data.AddRange(timeframeData);
            }

            return Ok(data);
        }

        [HttpGet("{symbol}/{date}")]
        public async Task<IActionResult> GetVolumeAsync(string symbol, DateTime date)
        {
            string path = $"{symbol}_{nameof(VolumeService)}_{date:yyyy-MM-dd}.json";
            var data = await dataKeeper.ReadDataAsync<VolumeLevelStorageItem>(path);
            return Ok(data);
        }

        [HttpGet("{symbol}/{timeFrame}")]
        public IActionResult GetLiveBarsSince(string symbol, int timeFrame, [FromQuery] DateTime since)
        {
            var candleService = _candleServices.FirstOrDefault(c => c.Symbol == symbol && c.TimeFrame == timeFrame);
            if (candleService?.DataKeep == null)
                return Ok(new List<BarStorageItem>());

            var bars = candleService.DataKeep
                .Where(b => b.Date > since)
                .OrderBy(b => b.Date)
                .ToList();

            return Ok(bars);
        }

        [HttpGet("{symbol}")]
        public IActionResult GetLiveBubblesSince(string symbol, [FromQuery] DateTime since)
        {
            var bubbleService = _bubbleServices.FirstOrDefault(b => b.Symbol == symbol);
            if (bubbleService?.DataKeep == null)
                return Ok(new List<BubbleStorageItem>());

            var bubbles = bubbleService.DataKeep
                .Where(b => b.Date > since)
                .OrderBy(b => b.Date)
                .ToList();

            return Ok(bubbles);
        }

        [HttpGet("{symbol}/{minDistance:double}")]
        public async Task<IActionResult> SetStructureDistanceAsync(string symbol, double minDistance)
        {
            // Só timeframes intraday: o 1440 (1D) tem distância própria
            // (SetStructureDistanceForTimeFrameAsync, issue #10).
            foreach (var structure in structureServices.Where(s => s.Symbol == symbol && s.TimeFrame != 1440))
            {
                await structure.SetMinDistance(minDistance);
            }

            // Resposta só intraday (sem 1440): o diário tem lista, range e
            // rotas próprios e nunca deve vazar para `_structures` do app.
            var data = await GetStructureAsync(symbol, DateTime.Today, minDistance) as OkObjectResult;
            if (data?.Value is List<StructureStorageItem> all)
                return Ok(all.Where(s => s.TimeFrame != 1440).ToList());
            return Ok(new List<StructureStorageItem>());
        }

        [HttpGet("{symbol}/{startDate}/{endDate}/{minDistance:double}")]
        public async Task<IActionResult> GetStructureRange(string symbol, DateTime startDate, DateTime endDate, double minDistance)
        {
            // Range INTRADAY (modo multi-day do app): lê os arquivos diários
            // dos timeframes < 1440 com a distância intraday. O 1440 nunca
            // entra aqui — o diário usa GetStructureHistory com o range
            // diário próprio.
            if (endDate < startDate)
                (startDate, endDate) = (endDate, startDate);
            var days = (int)Math.Min((endDate.Date - startDate.Date).TotalDays, 365);
            var all = new List<StructureStorageItem>();
            foreach (var timeFrame in Defaults.TimeFrames.Where(t => t != 1440))
            {
                for (var d = 0; d <= days; d++)
                {
                    var date = startDate.Date.AddDays(d);
                    var path = $"{symbol}_{nameof(StructureService)}_{timeFrame}MIN_{minDistance}_{date:yyyy-MM-dd}.json";
                    try
                    {
                        var day = await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);
                        if (day != null)
                            all.AddRange(day.Where(s => s.TimeFrame != 1440));
                    }
                    catch
                    {
                        // dias sem arquivo (fds/feriado/servidor novo): ignora
                    }
                }
            }
            return Ok(all.OrderBy(s => s.Date).ToList());
        }

        [HttpGet("{symbol}/{timeFrame:int}/{minDistance:double}")]
        public async Task<IActionResult> SetStructureDistanceForTimeFrameAsync(string symbol, int timeFrame, double minDistance)
        {
            var service = structureServices.FirstOrDefault(s => s.Symbol == symbol && s.TimeFrame == timeFrame);
            if (service == null) return NotFound();

            await service.SetMinDistance(minDistance);

            string path = $"{symbol}_{nameof(StructureService)}_{timeFrame}MIN_{minDistance}_{DateTime.Today:yyyy-MM-dd}.json";
            var data = await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);
            return Ok(data);
        }

        [HttpGet("{symbol}/{timeFrame:int}/{minDistance:double}")]
        public async Task<IActionResult> GetStructureHistory(string symbol, int timeFrame, double minDistance, [FromQuery] int days = 90)
        {
            days = Math.Clamp(days, 2, 365);
            var all = new List<StructureStorageItem>();
            for (var d = 0; d < days; d++)
            {
                var date = DateTime.Today.AddDays(-d);
                var path = $"{symbol}_{nameof(StructureService)}_{timeFrame}MIN_{minDistance}_{date:yyyy-MM-dd}.json";
                try
                {
                    var day = await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);
                    if (day != null)
                        all.AddRange(day);
                }
                catch
                {
                    // dias sem arquivo (fds/feriado/servidor novo): ignora
                }
            }
            // Fallback 1440: o arquivo leva a distância no nome; se o app
            // pediu uma distância sem nenhum arquivo (ex. slider arrastado
            // antes do Confirm), retorna o histórico da distância vigente no
            // serviço em vez de [] silencioso (precedente: GetStructureAsync).
            if (all.Count == 0)
            {
                var service = structureServices.FirstOrDefault(s => s.Symbol == symbol && s.TimeFrame == timeFrame);
                if (service != null && service._minDistanceUpdateBorder != minDistance)
                {
                    var current = service._minDistanceUpdateBorder;
                    for (var d = 0; d < days; d++)
                    {
                        var date = DateTime.Today.AddDays(-d);
                        var path = $"{symbol}_{nameof(StructureService)}_{timeFrame}MIN_{current}_{date:yyyy-MM-dd}.json";
                        try
                        {
                            var day = await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path);
                            if (day != null)
                                all.AddRange(day);
                        }
                        catch
                        {
                            // dias sem arquivo (fds/feriado/servidor novo): ignora
                        }
                    }
                }
            }
            return Ok(all.OrderBy(s => s.Date).ToList());
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetExtreme(string symbol,
            [FromQuery] DateTime? date = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var service = _extremeServices.FirstOrDefault(s => s.Symbol == symbol);
            if (service == null) return NotFound();

            var target = (date ?? DateTime.Today).Date;

            // Datas históricas: computa sob demanda a partir dos arquivos persistidos.
            if (target != DateTime.Today)
                return Ok(await service.ComputeForDate(dataKeeper, target, from, to));

            // Live day: if period filter provided, apply temporarily and return snapshot.
            if (from != null || to != null)
            {
                await service.SetPeriod(from, to);
                return Ok(service.GetSnapshot());
            }

            string path = $"{symbol}_{nameof(ExtremeService)}_{target:yyyy-MM-dd}.json";
            var data = await dataKeeper.ReadDataAsync<ExtremeStorageItem>(path);
            return Ok(data);
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetExtremeDaily(string symbol,
            [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
            [FromQuery] double noiseSensitivity = B3WM.Shared.Models.Defaults.Extreme.NoiseSensitivity,
            [FromQuery] double minimumProminence = B3WM.Shared.Models.Defaults.Extreme.MinimumProminence)
        {
            var service = _extremeServices.FirstOrDefault(s => s.Symbol == symbol);
            if (service == null) return NotFound();

            // Overlay diário estático: função pura, sem tocar no estado ao vivo
            // (período/snapshot/timers/broadcast do intraday ficam intactos).
            var toDate = (to ?? DateTime.Today).Date;
            var fromDate = (from ?? toDate.AddDays(-60)).Date;

            var options = new ExtremeDetectorOptions
            {
                NoiseSensitivity = noiseSensitivity,
                MinimumProminence = minimumProminence
            };
            return Ok(await service.ComputeDailyRange(dataKeeper, fromDate, toDate, options));
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetDailyProfile(string symbol,
            [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            // Volume Profile diário (issue #12): perfil agregado multi-dia a
            // partir dos arquivos diários do VolumeService (soma tick-a-tick
            // real de cada dia). Função pura, sem tocar no estado ao vivo.
            var toDate = (to ?? DateTime.Today).Date;
            var fromDate = (from ?? toDate.AddDays(-60)).Date;
            if (toDate < fromDate)
                (fromDate, toDate) = (toDate, fromDate);
            if ((toDate - fromDate).TotalDays > 365)
                fromDate = toDate.AddDays(-365);

            return Ok(await ExtremeService.BuildDailyProfileFromFiles(
                dataKeeper, symbol, fromDate, toDate));
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetPivotIntraday(string symbol,
            [FromQuery] DateTime? date = null, [FromQuery] int lineCount = 2)
        {
            // Pivot Tradicional do Profit no splitter superior (issue #14):
            // fonte HLC de D-1, linhas só sobre a sessão exibida (dia atual
            // ou último pregão). Função pura, sem tocar no estado ao vivo.
            var target = (date ?? DateTime.Today).Date;
            var n = PivotService.ClampLineCount(lineCount);
            var bars = await PivotService.ReadDailyBarsAsync(
                dataKeeper, symbol, target.AddDays(-60), target);
            var session = PivotService.ResolveSessionDate(bars, target);
            if (session == null)
                return Ok(new PivotStorageItem
                {
                    Symbol = symbol, Date = target, Source = "D-1", LineCount = n
                });
            var src = PivotService.ResolveIntradaySource(bars, session.Value);
            if (src == null)
                return Ok(new PivotStorageItem
                {
                    Symbol = symbol, Date = session.Value, Source = "D-1", LineCount = n
                });
            return Ok(new PivotStorageItem
            {
                Symbol = symbol,
                Date = session.Value,
                Source = "D-1",
                High = src.High,
                Low = src.Low,
                Close = src.Close,
                LineCount = n,
                Levels = PivotService.ComputeTraditional(src.High, src.Low, src.Close, n),
            });
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetPivotDaily(string symbol,
            [FromQuery] DateTime? date = null, [FromQuery] int lineCount = 2)
        {
            // Pivot Tradicional do Profit no widget diário (issue #14):
            // desloca 1 período superior — fonte HLC da semana anterior
            // (fiel ao Profit). Sem semana anterior, fallback para D-1.
            var target = (date ?? DateTime.Today).Date;
            var n = PivotService.ClampLineCount(lineCount);
            var bars = await PivotService.ReadDailyBarsAsync(
                dataKeeper, symbol, target.AddDays(-60), target);
            var session = PivotService.ResolveSessionDate(bars, target);
            if (session == null)
                return Ok(new PivotStorageItem
                {
                    Symbol = symbol, Date = target, Source = "W-1", LineCount = n
                });
            var weekly = PivotService.ResolveDailySource(bars, session.Value);
            double h, l, c;
            string source;
            if (weekly != null)
            {
                (h, l, c) = weekly.Value;
                source = "W-1";
            }
            else
            {
                var src = PivotService.ResolveIntradaySource(bars, session.Value);
                if (src == null)
                    return Ok(new PivotStorageItem
                    {
                        Symbol = symbol, Date = session.Value, Source = "W-1", LineCount = n
                    });
                h = src.High;
                l = src.Low;
                c = src.Close;
                source = "D-1";
            }
            return Ok(new PivotStorageItem
            {
                Symbol = symbol,
                Date = session.Value,
                Source = source,
                High = h,
                Low = l,
                Close = c,
                LineCount = n,
                Levels = PivotService.ComputeTraditional(h, l, c, n),
            });
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> SetExtremePeriodAsync(string symbol,
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] DateTime? date = null)
        {
            var service = _extremeServices.FirstOrDefault(s => s.Symbol == symbol);
            if (service == null) return NotFound();

            var target = (date ?? DateTime.Today).Date;
            if (target != DateTime.Today)
                return Ok(await service.ComputeForDate(dataKeeper, target, from, to));

            await service.SetPeriod(from, to);
            return Ok(service.GetSnapshot());
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> SetExtremeConfigAsync(string symbol,
            [FromQuery] double noiseSensitivity = B3WM.Shared.Models.Defaults.Extreme.NoiseSensitivity,
            [FromQuery] double minimumProminence = B3WM.Shared.Models.Defaults.Extreme.MinimumProminence,
            [FromQuery] DateTime? date = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var service = _extremeServices.FirstOrDefault(s => s.Symbol == symbol);
            if (service == null) return NotFound();

            var target = (date ?? DateTime.Today).Date;
            if (target == DateTime.Today)
            {
                await service.SetConfig(new ExtremeDetectorOptions
                {
                    NoiseSensitivity = noiseSensitivity,
                    MinimumProminence = minimumProminence
                });
                return Ok(service.GetSnapshot());
            }

            await service.SetConfig(new ExtremeDetectorOptions
            {
                NoiseSensitivity = noiseSensitivity,
                MinimumProminence = minimumProminence
            }, recomputeLive: false);
            await service.PersistConfig();
            return Ok(await service.ComputeForDate(dataKeeper, target, from, to));
        }
    }
}
