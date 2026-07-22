using B3WM.Client.Services;
using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Entity;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
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
        private readonly ILogger<DataController> _logger;

        public DataController(DataKeeperBase dataKeeper, IEnumerable<StructureService> structureServices, IEnumerable<CandleService> candleServices, IEnumerable<BubbleService> bubbleServices, ILogger<DataController> logger)
        {
            this.dataKeeper = dataKeeper;
            this.structureServices = structureServices;
            _candleServices = candleServices;
            _bubbleServices = bubbleServices;
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
                string path = $"{symbol}_{nameof(StructureService)}_{timeFrame}MIN_{minDistance}_{date:yyyy-MM-dd}.json";

                data.AddRange(await dataKeeper.ReadDataAsync<List<StructureStorageItem>>(path));
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
            foreach (var structure in structureServices.Where(s => s.Symbol == symbol))
            {
                await structure.SetMinDistance(minDistance);
            }

            return await GetStructureAsync(symbol, DateTime.Today, minDistance);
        }
    }
}
