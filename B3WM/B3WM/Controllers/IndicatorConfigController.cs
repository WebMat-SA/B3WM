using B3WM.Services.Indicators;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/indicator")]
    public class IndicatorConfigController : ControllerBase
    {
        private readonly IEnumerable<IIndicator> _indicators;

        public IndicatorConfigController(IEnumerable<IIndicator> indicators)
        {
            _indicators = indicators;
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
    }

    public class IndicatorConfigDto
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public Dictionary<string, double>? Parameters { get; set; }
    }
}
