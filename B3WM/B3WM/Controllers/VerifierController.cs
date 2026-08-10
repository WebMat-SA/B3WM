using B3WM.Services;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class VerifierController : ControllerBase
    {
        private readonly VerifierManager _manager;

        public VerifierController(VerifierManager manager)
        {
            _manager = manager;
        }

        [HttpPost]
        public IActionResult Start([FromBody] VerifierConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Symbol))
                return BadRequest("Config inválida");

            _manager.Start(config.Symbol, config.TimeFrame, config);
            return Ok();
        }

        [HttpPost]
        public IActionResult Stop([FromQuery] string symbol, [FromQuery] int timeFrame)
        {
            _manager.Stop(symbol, timeFrame);
            return Ok();
        }

        [HttpPost]
        public IActionResult Reset([FromQuery] string symbol, [FromQuery] int timeFrame)
        {
            _manager.Reset(symbol, timeFrame);
            return Ok();
        }

        [HttpGet]
        public ActionResult<VerifierState> State([FromQuery] string symbol, [FromQuery] int timeFrame)
        {
            return Ok(_manager.GetState(symbol, timeFrame));
        }

        [HttpGet]
        public async Task<ActionResult<List<VerifierLogDay>>> Export(
            [FromQuery] string symbol,
            [FromQuery] int timeFrame,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var days = await _manager.Export(symbol, timeFrame, from, to);
            return Ok(days);
        }
    }
}
