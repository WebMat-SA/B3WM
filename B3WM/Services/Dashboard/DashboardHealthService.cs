using B3WM.Shared.Entity;
using B3WM.Shared.Models.Dashboard;
using System.Text.Json;

namespace B3WM.Services.Dashboard
{
    /// <summary>
    /// Estado de saúde em memória consumido pelo dashboard. É alimentado pelos
    /// serviços (ticks, bubbles, heartbeats, erros) e lido pelo
    /// <c>DashboardController</c> no polling de 5s.
    ///
    /// Tudo é thread-safe: o DataHub grava em paralelo com os loops dos
    /// serviços e o controller lê a qualquer momento.
    /// </summary>
    public sealed class DashboardHealthService
    {
        public const int MaxRecentErrors = 200;
        private const int MaxUnknownAgents = 500;
        private static readonly TimeSpan PythonCacheTtl = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PythonProbeTimeout = TimeSpan.FromSeconds(2);

        private readonly object _lock = new();
        private readonly Dictionary<string, SymbolMetrics> _symbols = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UnknownAgentRecord> _unknownAgents = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<DashboardRecentError> _errors = new();
        private readonly Dictionary<string, DateTime> _heartbeats = new(StringComparer.OrdinalIgnoreCase);

        private readonly DateTime _startedAt = DateTime.Now;
        private readonly IServiceProvider? _services;
        private readonly IConfiguration? _config;

        private int _connectedClients;
        private long _totalConnections;

        private DateTime? _jevLastCallAt;
        private string? _jevLastResult;
        private long? _jevLastLatencyMs;
        private int? _jevLastHttpStatus;

        private DashboardPythonBridgeHealth? _pythonCache;
        private DateTime _pythonCacheAt = DateTime.MinValue;

        public DashboardHealthService(
            IServiceProvider? services = null,
            IConfiguration? config = null)
        {
            _services = services;
            _config = config;
        }

        public DateTime StartedAt => _startedAt;

        public string Version =>
            typeof(DashboardHealthService).Assembly.GetName().Version?.ToString() ?? "dev";

        // ------------------------------------------------------------------
        // Ticks / agentes
        // ------------------------------------------------------------------

        /// <summary>Registra um lote de ticks e detecta agentes fora do enum.</summary>
        public void RecordTicks(string symbol, Ticks2[] ticks)
        {
            if (string.IsNullOrWhiteSpace(symbol) || ticks == null || ticks.Length == 0)
                return;

            var now = DateTime.Now;
            var unknown = new List<int>();

            foreach (var tick in ticks)
            {
                if (tick == null) continue;
                if (!IsKnownAgent((int)tick.Buyer)) unknown.Add((int)tick.Buyer);
                if (!IsKnownAgent((int)tick.Seller)) unknown.Add((int)tick.Seller);
            }

            lock (_lock)
            {
                var metrics = GetOrCreateMetrics(symbol);
                metrics.FirstTickAt ??= now;
                metrics.LastTickAt = now;
                metrics.TotalTicks += ticks.Length;
                metrics.TickBatches.Enqueue((now, ticks.Length));
                PruneTickBatches(metrics, now);

                foreach (var code in unknown.Distinct())
                {
                    RecordUnknownAgentLocked(symbol, code, "", "tick", now);
                    metrics.LastUnknownAgentAt = now;
                }
            }
        }

        /// <summary>Registra uma bubble cujo agente não existe no enum.</summary>
        public void RecordBubbleAgent(string symbol, int agent)
        {
            if (string.IsNullOrWhiteSpace(symbol) || IsKnownAgent(agent))
                return;

            var now = DateTime.Now;
            lock (_lock)
            {
                RecordUnknownAgentLocked(symbol, agent, "", "bubble", now);
                var metrics = GetOrCreateMetrics(symbol);
                metrics.LastUnknownAgentAt = now;
            }
        }

        /// <summary>
        /// Nomes brutos que o ExtractorRTD não conseguiu mapear para o enum.
        /// O código é desconhecido (0), mas o nome permite adicioná-lo depois.
        /// </summary>
        public void RecordUnknownAgentNames(string symbol, IEnumerable<string>? rawNames)
        {
            if (string.IsNullOrWhiteSpace(symbol) || rawNames == null)
                return;

            var now = DateTime.Now;
            lock (_lock)
            {
                var metrics = GetOrCreateMetrics(symbol);
                foreach (var raw in rawNames.Take(200))
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var name = raw.Trim();
                    RecordUnknownAgentLocked(symbol, 0, name, "extractor", now);
                    metrics.LastUnknownAgentAt = now;
                }
            }
        }

        public static bool IsKnownAgent(int agent) =>
            Enum.IsDefined(typeof(Ticks2.Agents), agent);

        public static string AgentName(int agent) =>
            IsKnownAgent(agent) ? ((Ticks2.Agents)agent).ToString() : "";

        public IReadOnlyList<DashboardUnknownAgent> GetUnknownAgents()
        {
            lock (_lock)
            {
                return _unknownAgents.Values
                    .OrderByDescending(r => r.LastSeenAt)
                    .Select(r => new DashboardUnknownAgent
                    {
                        AgentCode = r.AgentCode,
                        AgentName = AgentName(r.AgentCode),
                        RawNames = string.IsNullOrWhiteSpace(r.RawName)
                            ? new List<string>()
                            : new List<string> { r.RawName },
                        Symbols = r.Symbols.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
                        Count = r.Count,
                        FirstSeenAt = r.FirstSeenAt,
                        LastSeenAt = r.LastSeenAt,
                        Source = r.Source,
                    })
                    .ToList();
            }
        }

        // ------------------------------------------------------------------
        // Métricas por símbolo
        // ------------------------------------------------------------------

        public DashboardSymbolMetrics GetSymbolMetrics(string symbol)
        {
            lock (_lock)
            {
                if (!_symbols.TryGetValue(symbol, out var metrics))
                    return new DashboardSymbolMetrics();

                PruneTickBatches(metrics, DateTime.Now);

                return new DashboardSymbolMetrics
                {
                    FirstTickAt = metrics.FirstTickAt,
                    LastTickAt = metrics.LastTickAt,
                    TotalTicks = metrics.TotalTicks,
                    TicksLastMinute = metrics.TickBatches.Sum(b => (long)b.Count),
                    LastUnknownAgentAt = metrics.LastUnknownAgentAt,
                };
            }
        }

        // ------------------------------------------------------------------
        // Heartbeats
        // ------------------------------------------------------------------

        public void RecordHeartbeat(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            lock (_lock) _heartbeats[key] = DateTime.Now;
        }

        public DateTime? GetHeartbeat(string key)
        {
            lock (_lock)
                return _heartbeats.TryGetValue(key, out var at) ? at : null;
        }

        // ------------------------------------------------------------------
        // Erros recentes
        // ------------------------------------------------------------------

        public void RecordError(string level, string category, string message,
            string? exceptionType = null, string? exceptionMessage = null)
        {
            var item = new DashboardRecentError
            {
                At = DateTime.Now,
                Level = level,
                Category = category,
                Message = Truncate(message, 2000),
                ExceptionType = exceptionType,
                ExceptionMessage = Truncate(exceptionMessage, 2000),
            };

            lock (_lock)
            {
                _errors.Enqueue(item);
                while (_errors.Count > MaxRecentErrors)
                    _errors.Dequeue();
            }
        }

        public IReadOnlyList<DashboardRecentError> GetRecentErrors(int max = 100)
        {
            lock (_lock)
            {
                return _errors
                    .Reverse()
                    .Take(Math.Clamp(max, 1, MaxRecentErrors))
                    .ToList();
            }
        }

        // ------------------------------------------------------------------
        // Jev
        // ------------------------------------------------------------------

        public void RecordJevCall(bool success, long latencyMs, int httpStatus, string result)
        {
            lock (_lock)
            {
                _jevLastCallAt = DateTime.Now;
                _jevLastResult = result;
                _jevLastLatencyMs = latencyMs;
                _jevLastHttpStatus = httpStatus;
            }
        }

        public (DateTime? LastCallAt, string? LastResult, long? LatencyMs, int? HttpStatus) GetJevCall()
        {
            lock (_lock)
                return (_jevLastCallAt, _jevLastResult, _jevLastLatencyMs, _jevLastHttpStatus);
        }

        // ------------------------------------------------------------------
        // Conexões SignalR
        // ------------------------------------------------------------------

        public void ConnectionOpened()
        {
            Interlocked.Increment(ref _connectedClients);
            Interlocked.Increment(ref _totalConnections);
        }

        public void ConnectionClosed()
        {
            if (Interlocked.Decrement(ref _connectedClients) < 0)
                Interlocked.Exchange(ref _connectedClients, 0);
        }

        public DashboardConnections GetConnections() => new()
        {
            ConnectedClients = Volatile.Read(ref _connectedClients),
            TotalConnections = Interlocked.Read(ref _totalConnections),
        };

        // ------------------------------------------------------------------
        // Bridge Python
        // ------------------------------------------------------------------

        public async Task<DashboardPythonBridgeHealth> GetPythonBridgeAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (_pythonCache != null && DateTime.Now - _pythonCacheAt < PythonCacheTtl)
                    return _pythonCache;
            }

            var result = await ProbePythonBridgeAsync(ct);

            lock (_lock)
            {
                _pythonCache = result;
                _pythonCacheAt = DateTime.Now;
            }

            return result;
        }

        private async Task<DashboardPythonBridgeHealth> ProbePythonBridgeAsync(CancellationToken ct)
        {
            var baseUrl = _config?.GetValue<string>("PythonService:BaseUrl") ?? "http://localhost:8000";
            var result = new DashboardPythonBridgeHealth
            {
                BaseUrl = baseUrl,
                CheckedAt = DateTime.Now,
            };

            var httpFactory = _services?.GetService<IHttpClientFactory>();
            if (httpFactory == null)
            {
                result.Status = DashboardStatus.Idle;
                result.Error = "HttpClientFactory indisponível";
                return result;
            }

            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(PythonProbeTimeout);

                var client = httpFactory.CreateClient("PythonService");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var response = await client.GetAsync("/health", linked.Token);
                sw.Stop();

                result.Reachable = response.IsSuccessStatusCode;
                result.LatencyMs = sw.ElapsedMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    result.Status = DashboardStatus.Warn;
                    result.Error = $"HTTP {(int)response.StatusCode}";
                    return result;
                }

                var json = await response.Content.ReadAsStringAsync(linked.Token);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("mt5_connected", out var mt5) &&
                    (mt5.ValueKind == JsonValueKind.True || mt5.ValueKind == JsonValueKind.False))
                {
                    result.Mt5Connected = mt5.GetBoolean();
                }

                result.Status = result.Mt5Connected == false
                    ? DashboardStatus.Warn
                    : DashboardStatus.Ok;

                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = DashboardStatus.Warn;
                result.Error = "timeout";
                return result;
            }
            catch (Exception ex)
            {
                result.Status = DashboardStatus.Warn;
                result.Error = ex.Message;
                return result;
            }
        }

        // ------------------------------------------------------------------
        // Diretório de dados
        // ------------------------------------------------------------------

        public DashboardDataDirectory GetDataDirectory()
        {
            const string path = "Data";
            var result = new DashboardDataDirectory { Path = path };

            try
            {
                result.Exists = Directory.Exists(path);
                if (!result.Exists) return result;

                result.Writable = CanWrite(path);
                // O diretório é atualizado a cada escrita atômica (tmp + move),
                // então não precisamos varrer milhares de arquivos a cada poll.
                result.LastWriteAt = Directory.GetLastWriteTime(path);
            }
            catch
            {
                // Probe é best-effort; o dashboard mostra o que foi possível ler.
            }

            return result;
        }

        private static bool CanWrite(string path)
        {
            try
            {
                var probe = Path.Combine(path, $".dashboard-probe-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------------
        // Regras de status
        // ------------------------------------------------------------------

        /// <summary>Pregão simplificado (seg–sex 09:00–18:00). Feriados não são considerados.</summary>
        public static bool IsMarketOpen(DateTime now) =>
            now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
            now.TimeOfDay >= TimeSpan.FromHours(9) &&
            now.TimeOfDay <= TimeSpan.FromHours(18);

        public static string StatusForAge(DateTime? last, DateTime now,
            double okSeconds, double warnSeconds, bool? marketOpen = null)
        {
            var open = marketOpen ?? IsMarketOpen(now);
            if (last == null)
                return open ? DashboardStatus.Error : DashboardStatus.Idle;

            var age = (now - last.Value).TotalSeconds;
            if (age <= okSeconds) return DashboardStatus.Ok;
            if (age <= warnSeconds) return DashboardStatus.Warn;
            return open ? DashboardStatus.Error : DashboardStatus.Idle;
        }

        /// <summary>
        /// Status para snapshots diários (estrutura 1D, topos/vales): não usa
        /// segundos, e sim dias úteis. Assim uma estrutura de sexta continua OK
        /// na segunda-feira e uma de 3 dias úteis atrás vira atenção.
        /// </summary>
        public static string StatusForDailySnapshot(DateTime? last, DateTime now,
            bool? marketOpen = null)
        {
            var open = marketOpen ?? IsMarketOpen(now);
            if (last == null)
                return open ? DashboardStatus.Error : DashboardStatus.Idle;

            var businessDays = BusinessDaysBetween(last.Value.Date, now.Date);
            if (businessDays <= 1) return DashboardStatus.Ok;
            if (businessDays <= 3) return DashboardStatus.Warn;
            return open ? DashboardStatus.Error : DashboardStatus.Idle;
        }

        private static int BusinessDaysBetween(DateTime from, DateTime to)
        {
            if (to <= from) return 0;

            var count = 0;
            for (var day = from.AddDays(1); day <= to; day = day.AddDays(1))
            {
                if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                    count++;
            }
            return count;
        }

        public static double? AgeSeconds(DateTime? last, DateTime now) =>
            last == null ? null : Math.Max(0, (now - last.Value).TotalSeconds);

        public static string WorstStatus(params string[] statuses)
        {
            if (statuses.Contains(DashboardStatus.Error)) return DashboardStatus.Error;
            if (statuses.Contains(DashboardStatus.Warn)) return DashboardStatus.Warn;
            if (statuses.Contains(DashboardStatus.Ok)) return DashboardStatus.Ok;
            return DashboardStatus.Idle;
        }

        // ------------------------------------------------------------------
        // Internos
        // ------------------------------------------------------------------

        private SymbolMetrics GetOrCreateMetrics(string symbol)
        {
            if (!_symbols.TryGetValue(symbol, out var metrics))
                _symbols[symbol] = metrics = new SymbolMetrics();
            return metrics;
        }

        private static void PruneTickBatches(SymbolMetrics metrics, DateTime now)
        {
            var cutoff = now.AddMinutes(-1);
            while (metrics.TickBatches.Count > 0 && metrics.TickBatches.Peek().At < cutoff)
                metrics.TickBatches.Dequeue();
        }

        private void RecordUnknownAgentLocked(string symbol, int code, string rawName,
            string source, DateTime now)
        {
            var key = $"{source}|{code}|{rawName.ToUpperInvariant()}";
            if (!_unknownAgents.TryGetValue(key, out var record))
            {
                // Evita crescimento ilimitado se um cliente malformado reportar
                // milhares de nomes diferentes.
                if (_unknownAgents.Count >= MaxUnknownAgents)
                    return;

                record = new UnknownAgentRecord
                {
                    AgentCode = code,
                    RawName = rawName,
                    Source = source,
                    FirstSeenAt = now,
                };
                _unknownAgents[key] = record;
            }

            record.Count++;
            record.LastSeenAt = now;
            record.Symbols.Add(symbol);
        }

        private static string Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= max ? value : value[..max] + "…";
        }

        private sealed class SymbolMetrics
        {
            public DateTime? FirstTickAt;
            public DateTime? LastTickAt;
            public long TotalTicks;
            public DateTime? LastUnknownAgentAt;
            public readonly Queue<(DateTime At, int Count)> TickBatches = new();
        }

        private sealed class UnknownAgentRecord
        {
            public int AgentCode;
            public string RawName = "";
            public string Source = "";
            public readonly HashSet<string> Symbols = new(StringComparer.OrdinalIgnoreCase);
            public long Count;
            public DateTime FirstSeenAt;
            public DateTime LastSeenAt;
        }
    }

    /// <summary>Cópia imutável das métricas de um símbolo.</summary>
    public sealed class DashboardSymbolMetrics
    {
        public DateTime? FirstTickAt { get; init; }
        public DateTime? LastTickAt { get; init; }
        public long TotalTicks { get; init; }
        public long TicksLastMinute { get; init; }
        public DateTime? LastUnknownAgentAt { get; init; }
    }
}
