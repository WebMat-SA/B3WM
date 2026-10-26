using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace B3WM.Services.AI
{
    /// <summary>
    /// Transporte HTTP da Jev (TypeSafe System One) — issue #16.
    /// Responsabilidade ÚNICA: autenticar, enviar state+questions, retry em
    /// 429/529 e devolver o JSON cru. Montagem da pergunta e leitura da
    /// resposta vivem em <see cref="JevPrompt"/> (camada de análise, usada
    /// pela JevAnalysisStrategy e pelo endpoint avulso).
    /// Chave via user-secrets <c>JevKey</c> (ou TypeSafe:ApiKey / env TYPESAFE_API_KEY).
    /// Log-only: nenhuma ordem é enviada ao MT5 (envio comentado no runner).
    /// </summary>
    public sealed class JevService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<JevService> _logger;

        public JevService(
            IHttpClientFactory httpFactory,
            IConfiguration config,
            ILogger<JevService> logger)
        {
            _httpFactory = httpFactory;
            _config = config;
            _logger = logger;
        }

        public string? ResolveApiKey()
        {
            return _config["JevKey"]
                ?? _config["TypeSafe:ApiKey"]
                ?? Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")
                ?? Environment.GetEnvironmentVariable("JevKey");
        }

        public bool HasKey => !string.IsNullOrWhiteSpace(ResolveApiKey());

        public string Model => _config["TypeSafe:Model"] ?? "jev-latest";

        /// <summary>
        /// Corpo HTTP montado uma única vez. O mesmo texto vai para o
        /// <see cref="StringContent"/> e para a auditoria (sem duplicação).
        /// </summary>
        public sealed record JevRequest(string Json, int StateChars);

        /// <summary>
        /// Monta o corpo exato da chamada: state como objeto, model e questions.
        /// </summary>
        public JevRequest BuildRequest(object state, object questions)
        {
            var stateChars = state is string s ? s.Length : JsonSerializer.Serialize(state).Length;
            var payload = new Dictionary<string, object?>
            {
                ["state"] = state,
                ["model"] = Model,
                ["questions"] = questions,
            };
            return new JevRequest(JsonSerializer.Serialize(payload), stateChars);
        }

        /// <summary>Resposta crua da Jev (interpretação em JevPrompt).</summary>
        public sealed class JevResponse
        {
            public string Model { get; set; } = "";
            public string Body { get; set; } = "";
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
            public int StateChars { get; set; }
            /// <summary>Auditoria (issue #23): tentativas, latência e status.</summary>
            public int Attempts { get; set; } = 1;
            public long LatencyMs { get; set; }
            public int HttpStatus { get; set; } = 200;
        }

        /// <summary>
        /// Monta o corpo e envia. O state vai como OBJETO JSON (não string)
        /// para o payload — o TypeSafe aceita objeto estruturado; embutir como
        /// string o escaparia. Lança InvalidOperationException sem chave,
        /// TimeoutException em timeout e HttpRequestException em HTTP não-2xx
        /// (após retries). O <see cref="JevRequest.Json"/> é exatamente o texto
        /// enviado no <see cref="StringContent"/>.
        /// </summary>
        public Task<JevResponse> AskAsync(
            object state,
            object questions,
            CancellationToken ct = default) =>
            AskAsync(BuildRequest(state, questions), ct);

        public async Task<JevResponse> AskAsync(
            JevRequest request,
            CancellationToken ct = default)
        {
            var apiKey = ResolveApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("JevKey ausente. Configure via 'dotnet user-secrets set JevKey <sua-chave>' ou env TYPESAFE_API_KEY.");

            var stateChars = request.StateChars;

            var client = _httpFactory.CreateClient("Jev");
            const int maxAttempts = 4; // 1 + 3 retries em 429/529
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var attemptsUsed = 1;
            HttpResponseMessage? resp = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                attemptsUsed = attempt;
                using var req = new HttpRequestMessage(HttpMethod.Post, "v1/systemone");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                req.Content = new StringContent(request.Json, Encoding.UTF8, "application/json");
                try
                {
                    resp = await client.SendAsync(req, ct);
                }
                catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    sw.Stop();
                    throw new JevTimeoutException(
                        $"Timeout chamando Jev (attempt {attempt}/{maxAttempts}).",
                        attempt, sw.ElapsedMilliseconds, ex);
                }

                if (resp.StatusCode is (HttpStatusCode)429 or (HttpStatusCode)529)
                {
                    if (attempt == maxAttempts)
                        break;
                    var delay = RetryDelay(resp, attempt);
                    _logger.LogWarning("Jev retornou {Status} (attempt {Attempt}/{Max}). Retry em {Delay}ms.", (int)resp.StatusCode, attempt, maxAttempts, delay.TotalMilliseconds);
                    resp.Dispose();
                    await Task.Delay(delay, ct);
                    continue;
                }
                break;
            }

            if (resp == null)
                throw new HttpRequestException("Sem resposta da Jev.");

            using (resp)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    sw.Stop();
                    throw new JevHttpException(
                        (int)resp.StatusCode, body, attemptsUsed, sw.ElapsedMilliseconds,
                        $"Jev HTTP {(int)resp.StatusCode}: {Truncate(body, 500)}");
                }

                var (model, inTok, outTok) = ReadUsage(body);
                sw.Stop();
                _logger.LogInformation("[jev] model={Model} in={In} out={Out} stateChars={Chars}",
                    model, inTok, outTok, stateChars);
                return new JevResponse
                {
                    Model = model,
                    Body = body,
                    InputTokens = inTok,
                    OutputTokens = outTok,
                    StateChars = stateChars,
                    Attempts = attemptsUsed,
                    LatencyMs = sw.ElapsedMilliseconds,
                    HttpStatus = (int)resp.StatusCode,
                };
            }
        }

        private (string Model, int In, int Out) ReadUsage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var model = root.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString() ?? Model : Model;
                int inTok = 0, outTok = 0;
                if (root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
                {
                    if (u.TryGetProperty("input_tokens", out var it) && it.ValueKind == JsonValueKind.Number)
                        inTok = it.GetInt32();
                    if (u.TryGetProperty("output_tokens", out var ot) && ot.ValueKind == JsonValueKind.Number)
                        outTok = ot.GetInt32();
                }
                return (model, inTok, outTok);
            }
            catch
            {
                return (Model, 0, 0);
            }
        }

        private static TimeSpan RetryDelay(HttpResponseMessage resp, int attempt)
        {
            if (resp.Headers.RetryAfter?.Delta is TimeSpan ra && ra > TimeSpan.Zero && ra < TimeSpan.FromSeconds(60))
                return ra;
            return TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s ?? string.Empty : s[..max] + "…";
    }

    /// <summary>
    /// HTTP não-2xx da Jev, com status/corpo/tentativas/latência para a
    /// auditoria. Herda de <see cref="HttpRequestException"/> para preservar
    /// os catches existentes.
    /// </summary>
    public sealed class JevHttpException : HttpRequestException
    {
        public int HttpStatus { get; }
        public string Body { get; }
        public int Attempts { get; }
        public long LatencyMs { get; }

        public JevHttpException(
            int httpStatus, string body, int attempts, long latencyMs, string message)
            : base(message)
        {
            HttpStatus = httpStatus;
            Body = body ?? "";
            Attempts = attempts;
            LatencyMs = latencyMs;
        }
    }

    /// <summary>
    /// Timeout da Jev, com tentativas/latência para a auditoria. Herda de
    /// <see cref="TimeoutException"/> para preservar os catches existentes.
    /// </summary>
    public sealed class JevTimeoutException : TimeoutException
    {
        public int Attempts { get; }
        public long LatencyMs { get; }

        public JevTimeoutException(
            string message, int attempts, long latencyMs, Exception? inner = null)
            : base(message, inner)
        {
            Attempts = attempts;
            LatencyMs = latencyMs;
        }
    }
}
