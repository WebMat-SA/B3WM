using B3WM.Shared.Entity;
using B3WM.Shared.Models.Dashboard;
using System.Collections.Concurrent;
using System.Text.Json;

namespace B3WM.Services.Dashboard
{
    /// <summary>
    /// Estado de saúde em memória consumido pelo dashboard. É alimentado pelos
    /// serviços (ticks, bubbles, heartbeats, erros) e lido pelo
    /// <c>DashboardController</c> no polling de 5s.
    ///
    /// O caminho quente (ticks/heartbeats) é livre de lock: usa apenas
    /// coleções concorrentes e <see cref="Interlocked"/>. Os locks dedicados
    /// existem só para manter invariantes exatos em rotas de baixa frequência
    /// (criação de agente desconhecido, buffer de erros e sonda do bridge).
    /// Nada aqui alimenta decisão de trading: o snapshot pode ser eventualmente
    /// consistente entre campos, mas nunca corrompido.
    /// </summary>
    public sealed class DashboardHealthService
    {
        public const int MaxRecentErrors = 200;
        public const int MaxUnknownAgents = 500;
        private static readonly TimeSpan PythonCacheTtl = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PythonProbeTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DataDirectoryCacheTtl = TimeSpan.FromSeconds(60);

        /// <summary>Set imutável dos códigos válidos — evita reflexão por tick.</summary>
        private static readonly HashSet<int> KnownAgents =
            new(Enum.GetValues<Ticks2.Agents>().Select(static a => (int)a));

        // Lock dedicado: só na criação de um registro de agente desconhecido
        // (primeira vez que a chave aparece), nunca em updates.
        private readonly object _agentLock = new();

        // Lock dedicado: só no enfileiramento/trim do buffer de erros.
        private readonly object _errorLock = new();

        // Lock dedicado: só em volta da sonda do bridge Python (cache de 10s).
        private readonly SemaphoreSlim _pythonLock = new(1, 1);

        // Lock dedicado: só na revalidação da sonda de escrita do diretório (cache de 60s).
        private readonly object _dataDirLock = new();

        private readonly ConcurrentDictionary<string, SymbolMetrics> _symbols = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, UnknownAgentRecord> _unknownAgents = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<DashboardRecentError> _errors = new();
        private readonly ConcurrentDictionary<string, long> _heartbeats = new(StringComparer.OrdinalIgnoreCase);

        private int _unknownAgentCount;

        private readonly DateTime _startedAt = DateTime.Now;
        private readonly IServiceProvider? _services;
        private readonly IConfiguration? _config;

        private int _connectedClients;
        private long _totalConnections;

        private JevState _jev = new(null, null, null, null);

        private PythonCache? _pythonCache;

        private DashboardDataDirectory? _dataDirCache;
        private long _dataDirAtTicks;

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
            List<int>? unknown = null;

            foreach (var tick in ticks)
            {
                if (tick == null) continue;

                // Código 0 = fallback do ExtractorRTD quando ele não mapeia o
                // nome. O nome bruto chega separado via ReportUnknownAgents;
                // registrar o 0 aqui só duplicaria a linha no dashboard.
                var buyer = (int)tick.Buyer;
                if (buyer != 0 && !IsKnownAgent(buyer))
                    (unknown ??= new List<int>()).Add(buyer);

                var seller = (int)tick.Seller;
                if (seller != 0 && !IsKnownAgent(seller))
                    (unknown ??= new List<int>()).Add(seller);
            }

            var metrics = _symbols.GetOrAdd(symbol, static _ => new SymbolMetrics());
            metrics.RecordBatch(now, ticks.Length);

            if (unknown is { Count: > 0 })
            {
                foreach (var code in unknown.Distinct())
                    RecordUnknownAgent(symbol, code, "", "tick", now);
                metrics.TouchUnknownAgent(now);
            }
        }

        /// <summary>Registra uma bubble cujo agente não existe no enum.</summary>
        public void RecordBubbleAgent(string symbol, int agent)
        {
            if (string.IsNullOrWhiteSpace(symbol) || IsKnownAgent(agent))
                return;

            var now = DateTime.Now;
            RecordUnknownAgent(symbol, agent, "", "bubble", now);
            _symbols.GetOrAdd(symbol, static _ => new SymbolMetrics()).TouchUnknownAgent(now);
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
            var metrics = _symbols.GetOrAdd(symbol, static _ => new SymbolMetrics());

            foreach (var raw in rawNames.Take(200))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                RecordUnknownAgent(symbol, 0, raw.Trim(), "extractor", now);
                metrics.TouchUnknownAgent(now);
            }
        }

        public static bool IsKnownAgent(int agent) => KnownAgents.Contains(agent);

        public static string AgentName(int agent) =>
            IsKnownAgent(agent) ? ((Ticks2.Agents)agent).ToString() : "";

        public IReadOnlyList<DashboardUnknownAgent> GetUnknownAgents()
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
                    Symbols = r.Symbols.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
                    Count = Interlocked.Read(ref r.Count),
                    FirstSeenAt = r.FirstSeenAt,
                    LastSeenAt = r.LastSeenAt,
                    Source = r.Source,
                })
                .ToList();
        }

        // ------------------------------------------------------------------
        // Métricas por símbolo
        // ------------------------------------------------------------------

        public DashboardSymbolMetrics GetSymbolMetrics(string symbol)
        {
            if (!_symbols.TryGetValue(symbol, out var metrics))
                return new DashboardSymbolMetrics();

            PruneTickBatches(metrics, DateTime.Now);

            long ticksLastMinute = 0;
            foreach (var (_, count) in metrics.TickBatches)
                ticksLastMinute += count;

            return new DashboardSymbolMetrics
            {
                FirstTickAt = metrics.FirstTickAt,
                LastTickAt = metrics.LastTickAt,
                TotalTicks = metrics.TotalTicks,
                TicksLastMinute = ticksLastMinute,
                LastUnknownAgentAt = metrics.LastUnknownAgentAt,
            };
        }

        // ------------------------------------------------------------------
        // Heartbeats
        // ------------------------------------------------------------------

        public void RecordHeartbeat(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            _heartbeats[key] = DateTime.Now.Ticks;
        }

        public DateTime? GetHeartbeat(string key) =>
            _heartbeats.TryGetValue(key, out var ticks)
                ? new DateTime(ticks, DateTimeKind.Local)
                : null;

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

            lock (_errorLock)
            {
                _errors.Enqueue(item);
                while (_errors.Count > MaxRecentErrors)
                    _errors.TryDequeue(out _);
            }
        }

        public IReadOnlyList<DashboardRecentError> GetRecentErrors(int max = 100)
        {
            var take = Math.Clamp(max, 1, MaxRecentErrors);
            var snapshot = _errors.ToArray(); // mais antigo -> mais recente

            var result = new List<DashboardRecentError>(Math.Min(take, snapshot.Length));
            for (var i = snapshot.Length - 1; i >= 0 && result.Count < take; i--)
                result.Add(snapshot[i]);

            return result;
        }

        // ------------------------------------------------------------------
        // Jev
        // ------------------------------------------------------------------

        public void RecordJevCall(bool success, long latencyMs, int httpStatus, string result)
        {
            Volatile.Write(ref _jev, new JevState(DateTime.Now, result, latencyMs, httpStatus));
        }

        public (DateTime? LastCallAt, string? LastResult, long? LatencyMs, int? HttpStatus) GetJevCall()
        {
            var state = Volatile.Read(ref _jev);
            return (state.LastCallAt, state.LastResult, state.LatencyMs, state.HttpStatus);
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
            var cache = Volatile.Read(ref _pythonCache);
            if (cache != null && DateTime.Now - cache.At < PythonCacheTtl)
                return cache.Value;

            await _pythonLock.WaitAsync(ct);
            try
            {
                cache = Volatile.Read(ref _pythonCache);
                if (cache != null && DateTime.Now - cache.At < PythonCacheTtl)
                    return cache.Value;

                var result = await ProbePythonBridgeAsync(ct);
                Volatile.Write(ref _pythonCache, new PythonCache(result, DateTime.Now));
                return result;
            }
            finally
            {
                _pythonLock.Release();
            }
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
            // A sonda de escrita faz I/O; cacheia por 60s para não escrever um
            // arquivo a cada poll de 5s. Fora do caminho de cálculos.
            var cached = Volatile.Read(ref _dataDirCache);
            if (cached != null &&
                DateTime.Now.Ticks - Interlocked.Read(ref _dataDirAtTicks) < DataDirectoryCacheTtl.Ticks)
                return cached;

            lock (_dataDirLock)
            {
                cached = Volatile.Read(ref _dataDirCache);
                if (cached != null &&
                    DateTime.Now.Ticks - _dataDirAtTicks < DataDirectoryCacheTtl.Ticks)
                    return cached;

                var result = ProbeDataDirectory();
                Volatile.Write(ref _dataDirCache, result);
                Interlocked.Exchange(ref _dataDirAtTicks, DateTime.Now.Ticks);
                return result;
            }
        }

        private static DashboardDataDirectory ProbeDataDirectory()
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

        private static void PruneTickBatches(SymbolMetrics metrics, DateTime now)
        {
            var cutoff = now.AddMinutes(-1).Ticks;
            while (metrics.TickBatches.TryPeek(out var head) && head.AtTicks < cutoff)
                metrics.TickBatches.TryDequeue(out _);
        }

        /// <summary>
        /// Cria/atualiza a linha do agente desconhecido. O lock dedicado só é
        /// adquirido quando a chave ainda não existe; updates são atômicos.
        /// </summary>
        private void RecordUnknownAgent(string symbol, int code, string rawName,
            string source, DateTime now)
        {
            var key = string.Concat(source, "|", code.ToString(), "|", rawName.ToUpperInvariant());

            if (!_unknownAgents.TryGetValue(key, out var record))
            {
                lock (_agentLock)
                {
                    if (!_unknownAgents.TryGetValue(key, out record))
                    {
                        // Evita crescimento ilimitado se um cliente malformado
                        // reportar milhares de nomes diferentes.
                        if (Volatile.Read(ref _unknownAgentCount) >= MaxUnknownAgents)
                            return;

                        record = new UnknownAgentRecord
                        {
                            AgentCode = code,
                            RawName = rawName,
                            Source = source,
                            FirstSeenAtTicks = now.Ticks,
                            LastSeenTicks = now.Ticks,
                        };
                        _unknownAgents[key] = record;
                        Interlocked.Increment(ref _unknownAgentCount);
                    }
                }
            }

            Interlocked.Increment(ref record.Count);
            Interlocked.Exchange(ref record.LastSeenTicks, now.Ticks);
            record.Symbols.TryAdd(symbol, 0);
        }

        private static DateTime? FromTicks(long ticks) =>
            ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Local);

        private static string Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= max ? value : value[..max] + "…";
        }

        private sealed class SymbolMetrics
        {
            private long _firstTickTicks;
            private long _lastTickTicks;
            private long _totalTicks;
            private long _lastUnknownAgentTicks;

            public readonly ConcurrentQueue<(long AtTicks, int Count)> TickBatches = new();

            public DateTime? FirstTickAt => FromTicks(Volatile.Read(ref _firstTickTicks));
            public DateTime? LastTickAt => FromTicks(Volatile.Read(ref _lastTickTicks));
            public DateTime? LastUnknownAgentAt => FromTicks(Volatile.Read(ref _lastUnknownAgentTicks));
            public long TotalTicks => Interlocked.Read(ref _totalTicks);

            public void RecordBatch(DateTime now, int count)
            {
                var ticks = now.Ticks;
                if (Volatile.Read(ref _firstTickTicks) == 0)
                    Interlocked.CompareExchange(ref _firstTickTicks, ticks, 0);

                Interlocked.Exchange(ref _lastTickTicks, ticks);
                Interlocked.Add(ref _totalTicks, count);
                TickBatches.Enqueue((ticks, count));
            }

            public void TouchUnknownAgent(DateTime now) =>
                Interlocked.Exchange(ref _lastUnknownAgentTicks, now.Ticks);
        }

        private sealed class UnknownAgentRecord
        {
            public int AgentCode;
            public string RawName = "";
            public string Source = "";
            public readonly ConcurrentDictionary<string, byte> Symbols =
                new(StringComparer.OrdinalIgnoreCase);
            public long Count;
            public long FirstSeenAtTicks;
            public long LastSeenTicks;

            public DateTime FirstSeenAt =>
                new(Volatile.Read(ref FirstSeenAtTicks), DateTimeKind.Local);

            public DateTime LastSeenAt =>
                new(Volatile.Read(ref LastSeenTicks), DateTimeKind.Local);
        }

        private sealed record JevState(
            DateTime? LastCallAt, string? LastResult, long? LatencyMs, int? HttpStatus);

        private sealed record PythonCache(DashboardPythonBridgeHealth Value, DateTime At);
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
