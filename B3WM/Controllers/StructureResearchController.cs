using B3WM.Services;
using B3WM.Services.Research;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    /// <summary>
    /// Leitura read-only do experimento ATR (issue #25). Não altera a
    /// estrutura de produção nem dispara recálculo.
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public sealed class StructureResearchController : ControllerBase
    {
        private readonly DataKeeperBase _keeper;
        private readonly StructureResearchOptions _options;

        public StructureResearchController(
            DataKeeperBase keeper,
            StructureResearchOptions options)
        {
            _keeper = keeper;
            _options = options;
        }

        [HttpGet]
        public IActionResult GetStatus() => Ok(new
        {
            enabled = _options.Enabled,
            atrPeriod = _options.AtrPeriod,
            multipliers = new
            {
                WINFUT = _options.AtrMultiplierWinfut,
                WDOFUT = _options.AtrMultiplierWdofut,
            },
            atrFloorTicks = _options.AtrFloorTicks,
            atrCapTicks = _options.AtrCapTicks,
            warmupDays = _options.WarmupDays,
            timeFrames = _options.TimeFrames,
            symbols = _options.Symbols,
            variants = StructureResearchVariants.All,
        });

        [HttpGet("{symbol}/{timeFrame:int}/{date:datetime}")]
        public async Task<IActionResult> GetPoints(
            string symbol, int timeFrame, DateTime date)
        {
            var all = new List<StructureResearchPoint>();
            foreach (var variant in StructureResearchVariants.All)
                all.AddRange(await ReadPointsAsync(symbol, timeFrame, date, variant));

            return Ok(all.OrderBy(p => p.Date).ThenBy(p => p.Variant).ToList());
        }

        /// <summary>Último ponto de cada variante no dia (polling rápido).</summary>
        [HttpGet("{symbol}/{timeFrame:int}")]
        public async Task<IActionResult> GetLatest(
            string symbol, int timeFrame, [FromQuery] DateTime? date = null)
        {
            var day = (date ?? DateTime.Today).Date;
            var latest = new List<StructureResearchPoint>();
            foreach (var variant in StructureResearchVariants.All)
            {
                var points = await ReadPointsAsync(symbol, timeFrame, day, variant);
                if (points.Count > 0)
                    latest.Add(points[^1]);
            }

            return Ok(latest);
        }

        [HttpGet("{symbol}/{timeFrame:int}/{date:datetime}")]
        public async Task<IActionResult> GetReport(
            string symbol, int timeFrame, DateTime date)
        {
            var byVariant = new Dictionary<string, IReadOnlyList<StructureResearchPoint>>();
            foreach (var variant in StructureResearchVariants.All)
            {
                var points = await ReadPointsAsync(symbol, timeFrame, date, variant);
                if (points.Count > 0)
                    byVariant[variant] = points;
            }

            var manualRange = byVariant.TryGetValue(StructureResearchVariants.Manual, out var manual)
                && manual.Count > 0
                    ? manual[0].RangeUsed
                    : Defaults.GetMinDistance(symbol);

            var report = StructureResearchReportBuilder.Build(
                symbol, timeFrame, date, byVariant,
                _options.FalseBreakBarsShort,
                _options.FalseBreakBarsLong,
                _options.AtrPeriod,
                _options.MultiplierFor(symbol),
                manualRange);

            return Ok(report);
        }

        private async Task<List<StructureResearchPoint>> ReadPointsAsync(
            string symbol, int timeFrame, DateTime date, string variant)
        {
            var path =
                $"Research/Structures/{symbol}_{timeFrame}MIN_{variant}_{date:yyyy-MM-dd}.json";
            var points = await _keeper.ReadDataAsync<List<StructureResearchPoint>>(path);
            return points ?? new List<StructureResearchPoint>();
        }
    }
}
