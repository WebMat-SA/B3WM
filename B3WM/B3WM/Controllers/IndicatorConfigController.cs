using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Services.Indicators;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/indicator")]
    public class IndicatorConfigController : ControllerBase
    {
        private readonly IEnumerable<IIndicator> _indicators;
        private readonly DataKeeperBase _dataKeeper;

        public IndicatorConfigController(IEnumerable<IIndicator> indicators, DataKeeperBase dataKeeper)
        {
            _indicators = indicators;
            _dataKeeper = dataKeeper;
        }

        [HttpGet("list")]
        public ActionResult<List<object>> List()
        {
            return Ok(_indicators.Select(i => new
            {
                i.Name,
                i.Enabled,
                i.NeedsBarCount,
                Parameters = i.Parameters
            }));
        }

        [HttpPost("config")]
        public IActionResult SetConfig([FromBody] IndicatorConfigDto dto)
        {
            var ind = _indicators.FirstOrDefault(i => i.Name == dto.Name);
            if (ind == null) return NotFound($"Indicator '{dto.Name}' not found");

            ind.Enabled = dto.Enabled;
            if (dto.Parameters != null)
            {
                foreach (var (key, value) in dto.Parameters)
                    ind.SetParameter(key, value);
            }

            return Ok();
        }

        [HttpGet("evaluate/{symbol}/{date}")]
        public async Task<ActionResult<List<IndicatorValue>>> Evaluate(string symbol, DateTime date)
        {
            var results = new List<IndicatorValue>();

            foreach (var timeFrame in Defaults.TimeFrames)
            {
                string path = $"{symbol}_{nameof(CandleService)}_{timeFrame}MIN_{date:yyyy-MM-dd}.json";
                var bars = await _dataKeeper.ReadDataAsync<List<BarStorageItem>>(path);
                if (bars == null || bars.Count == 0) continue;

                bars = bars.OrderBy(b => b.Date).ToList();

                foreach (var ind in _indicators.Where(i => i.Enabled))
                {
                    if (bars.Count < ind.NeedsBarCount) continue;

                    for (int i = ind.NeedsBarCount - 1; i < bars.Count; i++)
                    {
                        var bar = bars[i];
                        var history = bars.Skip(i - ind.NeedsBarCount + 1).Take(ind.NeedsBarCount).ToList();
                        var evalResults = ind.Evaluate(bar, history);

                        foreach (var r in evalResults)
                        {
                            results.Add(new IndicatorValue
                            {
                                Time = bar.Date,
                                Symbol = symbol,
                                IndicatorName = ind.Name,
                                Key = r.Key,
                                Value = r.Value,
                                PlotType = r.PlotType,
                                TimeFrame = timeFrame
                            });
                        }
                    }
                }
            }

            return Ok(results);
        }
    }

    public class IndicatorConfigDto
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public Dictionary<string, double>? Parameters { get; set; }
    }
}
