using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Services.Dashboard;
using B3WM.Services.Strategies;
using B3WM.Shared.Entity;
using B3WM.Shared.Extensions;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Dashboard;
using B3WM.Shared.Models.Strategies;
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

            // Indexa os serviços uma única vez por request (em vez de varrer as
            // coleções para cada símbolo).
            var index = DashboardServiceIndex.Build(
                _candleServices, _bubbleServices, _volumeServices,
                _structureServices, _extremeServices, _vwapServices, _tickChannels);

            // States() pega o lock do runner; chamar uma vez e filtrar por símbolo.
            var strategyStates = _strategies.States();

            foreach (var symbol in index.Symbols)
                health.Symbols.Add(await BuildSymbolHealthAsync(
                    symbol, now, marketOpen, index, strategyStates, ct));

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
            string symbol, DateTime now, bool marketOpen,
            DashboardServiceIndex index,
            IReadOnlyList<StrategySessionState> strategyStates,
            CancellationToken ct)
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

            result.Candles = index.CandlesFor(symbol)
                .Select(s => BuildCandleHealth(s, now, marketOpen))
                .ToList();

            result.Bubble = BuildBubbleHealth(index.BubbleFor(symbol), now, marketOpen);
            result.Volume = BuildVolumeHealth(index.VolumeFor(symbol), now, marketOpen);
            result.Structure = await BuildStructureHealthAsync(
                index.StructuresFor(symbol), symbol, now, marketOpen);
            result.Extreme = BuildExtremeHealth(index.ExtremeFor(symbol), now, marketOpen);
            result.Vwap = BuildVwapHealth(index.VwapFor(symbol), now, marketOpen);
            result.Orchestration = BuildOrchestrationHealth(index.ChannelFor(symbol), symbol, now, marketOpen);
            result.Strategies = BuildStrategyHealth(strategyStates, symbol);
            result.Charts = BuildChartData(symbol, index);

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

        private static DashboardChartData BuildChartData(string symbol, DashboardServiceIndex index)
        {
            var candles1 = index.CandleFor(symbol, 1);
            var candles5 = index.CandleFor(symbol, 5);
            var bubbles = index.BubbleFor(symbol)
                ?.GetRecentBubbles(40) ?? new List<BubbleStorageItem>();

            return new DashboardChartData
            {
                Candles1Min = candles1?.GetRecentBars(40).Select(ToChartPoint).ToList() ?? new(),
                Candles5Min = candles5?.GetRecentBars(40).Select(ToChartPoint).ToList() ?? new(),
                Bubbles = bubbles.Select(ToBubblePoint).ToList(),
            };
        }

        private static DashboardBubblePoint ToBubblePoint(BubbleStorageItem bubble) => new()
        {
            Date = bubble.Date,
            Agent = bubble.Agent,
            AgentName = DashboardHealthService.AgentName(bubble.Agent),
            Amount = bubble.Amount,
            Price = bubble.Price,
            ActionType = bubble.ActionType.ToString(),
        };

        private static DashboardCandlePoint ToChartPoint(BarStorageItem bar) => new()
        {
            Date = bar.Date,
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume,
        };

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

        private static DashboardBubbleHealth BuildBubbleHealth(
            BubbleService? service, DateTime now, bool marketOpen)
        {
            var bubble = service?.GetSnapshot();

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

        private static DashboardVolumeHealth BuildVolumeHealth(
            VolumeService? service, DateTime now, bool marketOpen)
        {
            var volume = service?.GetSnapshot();

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
            IEnumerable<StructureService> services,
            string symbol, DateTime now, bool marketOpen)
        {
            var list = new List<DashboardStructureHealth>();

            foreach (var service in services)
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

        private static DashboardValueHealth BuildExtremeHealth(
            ExtremeService? service, DateTime now, bool marketOpen)
        {
            var extreme = service?.GetSnapshot();

            var lastAt = extreme == null || extreme.Date == default ? (DateTime?)null : extreme.Date;

            return new DashboardValueHealth
            {
                Status = DashboardHealthService.StatusForDailySnapshot(lastAt, now, marketOpen),
                LastAt = lastAt,
                AgeSeconds = DashboardHealthService.AgeSeconds(lastAt, now),
                Count = extreme?.Extremes?.Count ?? 0,
            };
        }

        private static DashboardValueHealth BuildVwapHealth(
            VwapService? service, DateTime now, bool marketOpen)
        {
            var vwap = service?.GetSnapshot();

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
            TickChannelService? channel, string symbol, DateTime now, bool marketOpen)
        {
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

        private static DashboardStrategyHealth BuildStrategyHealth(
            IReadOnlyList<StrategySessionState> states, string symbol)
        {
            var sessions = states
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

        /// <summary>
        /// Índice símbolo → serviços montado uma vez por request. Evita varreduras
        /// repetidas das coleções de DI a cada símbolo no polling.
        /// </summary>
        private sealed class DashboardServiceIndex
        {
            private readonly Dictionary<string, CandleService[]> _candles;
            private readonly Dictionary<string, BubbleService> _bubbles;
            private readonly Dictionary<string, VolumeService> _volumes;
            private readonly Dictionary<string, StructureService[]> _structures;
            private readonly Dictionary<string, ExtremeService> _extremes;
            private readonly Dictionary<string, VwapService> _vwaps;
            private readonly Dictionary<string, TickChannelService> _channels;

            public List<string> Symbols { get; }

            private DashboardServiceIndex(
                Dictionary<string, CandleService[]> candles,
                Dictionary<string, BubbleService> bubbles,
                Dictionary<string, VolumeService> volumes,
                Dictionary<string, StructureService[]> structures,
                Dictionary<string, ExtremeService> extremes,
                Dictionary<string, VwapService> vwaps,
                Dictionary<string, TickChannelService> channels,
                List<string> symbols)
            {
                _candles = candles;
                _bubbles = bubbles;
                _volumes = volumes;
                _structures = structures;
                _extremes = extremes;
                _vwaps = vwaps;
                _channels = channels;
                Symbols = symbols;
            }

            public static DashboardServiceIndex Build(
                IEnumerable<CandleService> candles,
                IEnumerable<BubbleService> bubbles,
                IEnumerable<VolumeService> volumes,
                IEnumerable<StructureService> structures,
                IEnumerable<ExtremeService> extremes,
                IEnumerable<VwapService> vwaps,
                IEnumerable<TickChannelService> channels)
            {
                var candleMap = candles
                    .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderBy(s => s.TimeFrame).ToArray(),
                        StringComparer.OrdinalIgnoreCase);

                var symbols = candleMap.Keys
                    .Concat(bubbles.Select(b => b.Symbol))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new DashboardServiceIndex(
                    candleMap,
                    SingleBySymbol(bubbles),
                    SingleBySymbol(volumes),
                    structures
                        .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.OrderBy(s => s.TimeFrame).ToArray(),
                            StringComparer.OrdinalIgnoreCase),
                    SingleBySymbol(extremes),
                    SingleBySymbol(vwaps),
                    SingleBySymbol(channels),
                    symbols);
            }

            private static Dictionary<string, T> SingleBySymbol<T>(IEnumerable<T> services)
                where T : ISymbolable =>
                services
                    .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            public IEnumerable<CandleService> CandlesFor(string symbol) =>
                _candles.TryGetValue(symbol, out var list) ? list : Array.Empty<CandleService>();

            public CandleService? CandleFor(string symbol, int timeFrame) =>
                _candles.TryGetValue(symbol, out var list)
                    ? list.FirstOrDefault(c => c.TimeFrame == timeFrame)
                    : null;

            public BubbleService? BubbleFor(string symbol) => _bubbles.GetValueOrDefault(symbol);
            public VolumeService? VolumeFor(string symbol) => _volumes.GetValueOrDefault(symbol);
            public IEnumerable<StructureService> StructuresFor(string symbol) =>
                _structures.TryGetValue(symbol, out var list) ? list : Array.Empty<StructureService>();
            public ExtremeService? ExtremeFor(string symbol) => _extremes.GetValueOrDefault(symbol);
            public VwapService? VwapFor(string symbol) => _vwaps.GetValueOrDefault(symbol);
            public TickChannelService? ChannelFor(string symbol) => _channels.GetValueOrDefault(symbol);
        }
    }
}
