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
        private readonly DataKeeperBase _dataKeeper;

        public BacktestController(BacktestEngine engine, DataKeeperBase dataKeeper)
        {
            _engine = engine;
            _dataKeeper = dataKeeper;
        }

        [HttpPost]
        public async Task<ActionResult<BacktestResult>> Run([FromBody] BacktestConfig config)
        {
            if (config.StartDate >= config.EndDate)
                return BadRequest("StartDate must be before EndDate");

            if (config.StopLossPoints <= 0 && config.TakeProfitPoints <= 0)
                return BadRequest("At least StopLossPoints or TakeProfitPoints must be > 0");

            IStrategy strategy = config.StrategyName switch
            {
                StrategyType.Breakout => new SimpleBreakoutStrategy(config.LookbackPeriod),
                StrategyType.SmartBreakout => new SmartBreakoutStrategy(_dataKeeper, config),
                _ => throw new ArgumentException($"Unknown strategy: {config.StrategyName}")
            };

            var result = await _engine.Run(config, strategy);
            return Ok(result);
        }
    }
}
