using B3WM.Services;
using B3WM.Services.Backtest;
using B3WM.Shared.Models.Backtest;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class BacktestController : ControllerBase
    {
        private readonly BacktestEngine _engine;
        private readonly IStrategyFactory _strategies;

        public BacktestController(BacktestEngine engine, IStrategyFactory strategies)
        {
            _engine = engine;
            _strategies = strategies;
        }

        [HttpPost]
        public async Task<ActionResult<BacktestResult>> Run([FromBody] BacktestConfig config, CancellationToken ct)
        {
            if (config.StartDate >= config.EndDate)
                return BadRequest("StartDate must be before EndDate");

            if (config.StopLossPoints <= 0 && config.TakeProfitPoints <= 0)
                return BadRequest("At least StopLossPoints or TakeProfitPoints must be > 0");

            IStrategy strategy;
            try
            {
                strategy = _strategies.Create(config);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }

            var result = await _engine.Run(config, strategy, ct);
            return Ok(result);
        }
    }
}
