using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Services.Dashboard;
using B3WM.Services.Strategies;
using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class DashboardController : ControllerBase
    {
        private readonly IEnumerable<CandleService> _candleServices;
        private readonly IEnumerable<BubbleService> _bubbleServices;
        private readonly IEnumerable<VolumeService> _volumeServices;
        private readonly IEnumerable<StructureService> _structureServices;
        private readonly IEnumerable<ExtremeService> _extremeServices;
        private readonly IEnumerable<VwapService> _vwapServices;
        private readonly IEnumerable<TickChannelService> _tickChannels;
        private readonly DashboardHealthService _health;
        private readonly StructureRangeResolver _rangeResolver;
        private readonly StrategyRunner _strategies;
        private readonly JevService _jev;
        private readonly IHostEnvironment _environment;

        public DashboardController(
            IEnumerable<CandleService> candleServices,
            IEnumerable<BubbleService> bubbleServices,
            IEnumerable<VolumeService> volumeServices,
            IEnumerable<StructureService> structureServices,
            IEnumerable<ExtremeService> extremeServices,
            IEnumerable<VwapService> vwapServices,
            IEnumerable<TickChannelService> tickChannels,
            DashboardHealthService health,
            StructureRangeResolver rangeResolver,
            StrategyRunner strategies,
            JevService jev,
            IHostEnvironment environment)
        {
            _candleServices = candleServices;
            _bubbleServices = bubbleServices;
            _volumeServices = volumeServices;
            _structureServices = structureServices;
            _extremeServices = extremeServices;
            _vwapServices = vwapServices;
            _tickChannels = tickChannels;
            _health = health;
            _rangeResolver = rangeResolver;
            _strategies = strategies;
            _jev = jev;
            _environment = environment;
        }

        /// <summary>Snapshot bruto legado (mantido para compatibilidade).</summary>
        [HttpGet]
        public IActionResult Get()
        {
            List<object> data = new();
            foreach (var iprocess in _candleServices)
                data.Add(iprocess.GetSnapshot());

            foreach (var iprocess in _bubbleServices)
                data.Add(iprocess.GetSnapshot());

            foreach (var iprocess in _volumeServices)
                data.Add(iprocess.GetSnapshot());

            return Ok(data);
        }

        /// <summary>
        /// Saúde consolidada de todos os serviços. Consumido pelo dashboard
        /// estático em /dashboard/ a cada 5s.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Health(CancellationToken ct)
        {
            var now = DateTime.Now;
            var marketOpen = DashboardHealthService.IsMarketOpen(now);

            var health = new DashboardHealth
            {
                GeneratedAt = now,
                ServerTime = now,
                UptimeSeconds = (long)Math.Max(0, (now - _health.StartedAt).TotalSeconds),
                Environment = _environment.EnvironmentName,
                Version = _health.Version,
                DataDirectory = _health.GetDataDirectory(),
                Connections = _health.GetConnections(),
                UnknownAgents = _health.GetUnknownAgents().ToList(),
                RecentErrors = _health.GetRecentErrors(100).ToList(),
            };

            var symbols = _candleServices
                .Select(s => s.Symbol)
                .Concat(_bubbleServices.Select(s => s.Symbol))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var symbol in symbols)
                health.Symbols.Add(await BuildSymbolHealthAsync(symbol, now, marketOpen, ct));

            health.Integrations = await BuildIntegrationsAsync(ct);
            health.Overall = BuildOverall(health, now);

            return Ok(health);
        }

        /// <summary>Lista o enum de agentes + os desconhecidos vistos em runtime.</summary>
        [HttpGet]
        public IActionResult Agents()
        {
            var known = System.Enum.GetValues<Ticks2.Agents>()
                .Select(a => new DashboardAgentInfo
                {
                    Code = (int)a,
                    Name = a.Description() ?? a.ToString(),
                    Known = true,
                })
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var unknown = _health.GetUnknownAgents()
                .Select(u => new DashboardAgentInfo
                {
                    Code = u.AgentCode,
                    Name = string.IsNullOrWhiteSpace(u.AgentName)
                        ? string.Join(", ", u.RawNames)
                        : u.AgentName,
                    Known = false,
                })
                .ToList();

            return Ok(new { known, unknown });
        }

        private async Task<DashboardSymbolHealth> BuildSymbolHealthAsync(
            string symbol, DateTime now, bool marketOpen, CancellationToken ct)
        {
            var result = new DashboardSymbolHealth { Symbol = symbol };

            var metrics = _health.GetSymbolMetrics(symbol);
            var dataSourceAge = DashboardHealthService.AgeSeconds(metrics.LastTickAt, now);
            result.DataSource = new DashboardDataSourceHealth
            {
                Status = DashboardHealthService.StatusForAge(metrics.LastTickAt, now, 60, 300, marketOpen),
                LastTickAt = metrics.LastTickAt,
                FirstTickAt = metrics.FirstTickAt,
                AgeSeconds = dataSourceAge,
                TicksLastMinute = metrics.TicksLastMinute,
                TotalTicks = metrics.TotalTicks,
                LastUnknownAgentAt = metrics.LastUnknownAgentAt,
            };

            result.Candles = _candleServices
                .Where(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.TimeFrame)
                .Select(s => BuildCandleHealth(s, now, marketOpen))
                .ToList();

            result.Bubble = BuildBubbleHealth(symbol, now, marketOpen);
            result.Volume = BuildVolumeHealth(symbol, now, marketOpen);
            result.Structure = await BuildStructureHealthAsync(symbol, now, marketOpen);
            result.Extreme = BuildExtremeHealth(symbol, now, marketOpen);
            result.Vwap = BuildVwapHealth(symbol, now, marketOpen);
            result.Orchestration = BuildOrchestrationHealth(symbol, now, marketOpen);
            result.Strategies = BuildStrategyHealth(symbol);

            var statuses = new List<string>
            {
                result.DataSource.Status,
                result.Bubble.Status,
                result.Volume.Status,
                result.Extreme.Status,
                result.Vwap.Status,
                result.Orchestration.Status,
                result.Strategies.Status,
            };
            statuses.AddRange(result.Candles.Select(c => c.Status));
            statuses.AddRange(result.Structure.Select(s => s.Status));

            result.Status = DashboardHealthService.WorstStatus(statuses.ToArray());
            return result;
        }

        private static DashboardCandleHealth BuildCandleHealth(
            CandleService service, DateTime now, bool marketOpen)
        {
            var bar = service.GetSnapshot();
            var lastAt = bar.Date == default ? (DateTime?)null : bar.Date;
            var tf = service.TimeFrame;
            var ok = tf == 1440 ? 86400 : Math.Max(120, tf * 60 * 2);
            var warn = tf == 1440 ? 172800 : ok * 3;

            return new DashboardCandleHealth
            {
                TimeFrame = tf,
                Status = DashboardHealthService.StatusForAge(lastAt, now, ok, warn, marketOpen),
                LastBarAt = lastAt,
                AgeSeconds = DashboardHealthService.AgeSeconds(lastAt, now),
                Close = bar.Close,
                High = bar.High,
                Low = bar.Low,
                Volume = bar.Volume,
                IsToday = lastAt?.Date == now.Date,
            };
        }

        private DashboardBubbleHealth BuildBubbleHealth(string symbol, DateTime now, bool marketOpen)
        {
            var bubble = _bubbleServices
                .FirstOrDefault(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                ?.GetSnapshot();

            var hasBubble = bubble != null && bubble.Date != default;
            var lastAt = hasBubble ? bubble!.Date : (DateTime?)null;

            return new DashboardBubbleHealth
            {
                Status = DashboardHealthService.StatusForAge(lastAt, now, 300, 900, marketOpen),
                LastAt = lastAt,
                AgeSeconds = DashboardHealthService.AgeSeconds(lastAt, now),
                Agent = hasBubble ? bubble!.Agent : null,
                AgentName = hasBubble ? DashboardHealthService.AgentName(bubble!.Agent) : "",
                Amount = hasBubble ? bubble!.Amount : null,
                Price = hasBubble ? bubble!.Price : null,
                ActionType = hasBubble ? bubble!.ActionType.ToString() : "",
            };
        }

        private DashboardVolumeHealth BuildVolumeHealth(string symbol, DateTime now, bool marketOpen)
        {
            var volume = _volumeServices
                .FirstOrDefault(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                ?.GetSnapshot();

            var hasVolume = volume?.Volumes is { Count: > 0 };
            var lastAt = hasVolume ? volume!.Date : (DateTime?)null;

            return new DashboardVolumeHealth
            {
                Status = DashboardHealthService.StatusForAge(lastAt, now, 300, 900, marketOpen),
                LastAt = lastAt,
                AgeSeconds = DashboardHealthService.AgeSeconds(lastAt, now),
                Levels = hasVolume ? volume!.Volumes.Count : 0,
                TotalVolume = hasVolume ? volume!.Volumes.Sum(v => v.Total) : 0,
            };
        }

        private async Task<List<DashboardStructureHealth>> BuildStructureHealthAsync(
            string symbol, DateTime now, bool marketOpen)
        {
            var list = new List<DashboardStructureHealth>();

            foreach (var service in _structureServices
                .Where(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.TimeFrame))
            {
                var last = service.GetLastStructure();
                var lastAt = last == null || last.Date == default ? (DateTime?)null : last.Date;
                var tf = service.TimeFrame;
                var ok = tf == 1440 ? 86400 : Math.Max(180, tf * 60 * 3);
                var warn = tf == 1440 ? 172800 : ok * 2;

                double? resolved = null;
                try
                {
                    resolved = await _rangeResolver.ResolveAsync(symbol, tf == 1440);
                }
                catch
                {
                    // Range deduzido é informativo; não derruba o dashboard.
                }

                list.Add(new DashboardStructureHealth
                {
                    TimeFrame = tf,
                    Status = tf == 1440
                        ? DashboardHealthService.StatusForDailySnapshot(lastAt, now, marketOpen)
                        : DashboardHealthService.StatusForAge(lastAt, now, ok, warn, marketOpen),
                    Range = service._minDistanceUpdateBorder,
                    ResolvedRange = resolved,
                    DefaultRange = tf == 1440
                        ? Defaults.GetMinDistanceDaily(symbol)
                        : Defaults.GetMinDistance(symbol),
                    LastAt = lastAt,
                    AgeSeconds = DashboardHealthService.AgeSeconds(lastAt, now),
                    UpBorder = last?.UpBorder,
                    DownBorder = last?.DownBorder,
                    UpAuxBorder = last?.UpAuxBorder,
                    DownAuxBorder = last?.DownAuxBorder,
                    IsToday = lastAt?.Date == now.Date,
                });
            }

            return list;
        }

        private DashboardValueHealth BuildExtremeHealth(string symbol, DateTime now, bool marketOpen)
        {
            var extreme = _extremeServices
                .FirstOrDefault(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                ?.GetSnapshot();

            var lastAt = extreme == null || extreme.Date == default ? (DateTime?)null : extreme.Date;

            return new DashboardValueHealth
            {
                Status = DashboardHealthService.StatusForDailySnapshot(lastAt, now, marketOpen),
                LastAt = lastAt,
                AgeSeconds = DashboardHealthService.AgeSeconds(lastAt, now),
                Count = extreme?.Extremes?.Count ?? 0,
            };
        }

        private DashboardValueHealth BuildVwapHealth(string symbol, DateTime now, bool marketOpen)
        {
            var vwap = _vwapServices
                .FirstOrDefault(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                ?.GetSnapshot();

            var lastAt = vwap == null || vwap.Minute == default ? (DateTime?)null : vwap.Minute;

            return new DashboardValueHealth
            {
                Status = DashboardHealthService.StatusForAge(lastAt, now, 300, 900, marketOpen),
                LastAt = lastAt,
                AgeSeconds = DashboardHealthService.AgeSeconds(lastAt, now),
                Value = vwap?.Vwap,
            };
        }

        private DashboardOrchestrationHealth BuildOrchestrationHealth(
            string symbol, DateTime now, bool marketOpen)
        {
            var channel = _tickChannels
                .FirstOrDefault(c => string.Equals(c.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

            var tickProcessor = _health.GetHeartbeat($"{symbol}:tickprocessor");
            var throttling = _health.GetHeartbeat($"{symbol}:throttling");

            var status = DashboardHealthService.WorstStatus(
                DashboardHealthService.StatusForAge(tickProcessor, now, 10, 30, marketOpen),
                DashboardHealthService.StatusForAge(throttling, now, 10, 30, marketOpen));

            return new DashboardOrchestrationHealth
            {
                Status = status,
                TickChannelQueued = channel?.Channel.Reader.Count ?? 0,
                TickProcessorHeartbeatAt = tickProcessor,
                ThrottlingHeartbeatAt = throttling,
                TickProcessorAgeSeconds = DashboardHealthService.AgeSeconds(tickProcessor, now),
                ThrottlingAgeSeconds = DashboardHealthService.AgeSeconds(throttling, now),
            };
        }

        private DashboardStrategyHealth BuildStrategyHealth(string symbol)
        {
            var sessions = _strategies.States()
                .Where(s => string.Equals(s.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var lastDecisionAt = sessions
                .SelectMany(s => s.ReportItems)
                .Select(r => (DateTime?)r.At)
                .DefaultIfEmpty(null)
                .Max();

            var active = sessions.Count(s => !s.Paused);
            var paused = sessions.Count(s => s.Paused);

            return new DashboardStrategyHealth
            {
                Status = active > 0
                    ? DashboardStatus.Ok
                    : sessions.Count > 0 ? DashboardStatus.Warn : DashboardStatus.Idle,
                ActiveSessions = active,
                PausedSessions = paused,
                TotalSessions = sessions.Count,
                Decisions = sessions.Sum(s => s.Decisions),
                LastDecisionAt = lastDecisionAt,
            };
        }

        private async Task<DashboardIntegrations> BuildIntegrationsAsync(CancellationToken ct)
        {
            var (lastCallAt, lastResult, latency, httpStatus) = _health.GetJevCall();

            var jev = new DashboardJevHealth
            {
                Enabled = true,
                HasKey = _jev.HasKey,
                Model = _jev.Model,
                LastCallAt = lastCallAt,
                LastResult = lastResult,
                LastLatencyMs = latency,
                LastHttpStatus = httpStatus,
                Status = !_jev.HasKey
                    ? DashboardStatus.Warn
                    : lastResult is not null && lastResult != "ok"
                        ? DashboardStatus.Warn
                        : DashboardStatus.Ok,
            };

            var python = await _health.GetPythonBridgeAsync(ct);

            return new DashboardIntegrations { Jev = jev, PythonBridge = python };
        }

        private static string BuildOverall(DashboardHealth health, DateTime now)
        {
            var statuses = new List<string>
            {
                health.Integrations.Jev.Status,
                health.Integrations.PythonBridge.Status,
            };

            statuses.AddRange(health.Symbols.Select(s => s.Status));

            if (health.UnknownAgents.Count > 0)
                statuses.Add(DashboardStatus.Warn);

            var recentCutoff = now.AddMinutes(-5);
            if (health.RecentErrors.Any(e =>
                    e.At >= recentCutoff &&
                    (e.Level == "Error" || e.Level == "Critical")))
                statuses.Add(DashboardStatus.Warn);

            return DashboardHealthService.WorstStatus(statuses.ToArray());
        }
    }
}
