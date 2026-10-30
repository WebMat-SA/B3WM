using B3WM.Services.AI;
using B3WM.Services.Screen;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    /// <summary>
    /// Endpoint avulso da Jev (debug): monta o snapshot WYSIWYG a partir do
    /// spec cru e repassa à Jev. Só log — nenhuma ordem (envio comentado).
    /// Resolução delegada ao ScreenStateBuilder (reuso pelas strategies).
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AiController : ControllerBase
    {
        private readonly JevService _jev;
        private readonly ScreenStateBuilder _builder;
        private readonly JevAuditStore _audit;
        private readonly B3WM.Services.DataKeeperBase _keeper;
        private readonly ILogger<AiController> _logger;

        public AiController(
            JevService jev,
            ScreenStateBuilder builder,
            JevAuditStore audit,
            B3WM.Services.DataKeeperBase keeper,
            ILogger<AiController> logger)
        {
            _jev = jev;
            _builder = builder;
            _audit = audit;
            _keeper = keeper;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Status()
        {
            // Nunca vazar o valor da chave — só se existe.
            return Ok(new Dictionary<string, object?> { ["enabled"] = true, ["hasKey"] = _jev.HasKey, ["model"] = _jev.Model });
        }

        [HttpPost]
        public async Task<IActionResult> Analyze([FromBody] JevAnalyzeRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Symbol))
                return BadRequest("Symbol é obrigatório (WINFUT/WDOFUT).");
            req.TimeFrame = req.TimeFrame <= 0 ? 5 : req.TimeFrame;
            req.LookbackCandles = Math.Clamp(req.LookbackCandles, 5, 200);
            req.LookbackBubbles = Math.Clamp(req.LookbackBubbles, 0, 200);

            var displayDate = (req.DisplayDate?.Date ?? DateTime.Today);
            var specRaw = req.ScreenConfig?.GetRawText() ?? "{}";
            using var specDoc = System.Text.Json.JsonDocument.Parse(specRaw);
            var daily = ScreenSpec.Daily(specDoc.RootElement);
            var snap = await _builder.BuildAsync(new ScreenStateRequest
            {
                Symbol = req.Symbol,
                TimeFrame = req.TimeFrame,
                DisplayDate = displayDate,
                VisibleTimeFrames = req.VisibleTimeFrames?.Where(t => t > 0).Distinct().ToList() ?? new() { req.TimeFrame },
                Spec = specDoc.RootElement.Clone(),
                ProfileFrom = req.ProfileFrom,
                ProfileTo = req.ProfileTo,
                DailyFrom = req.DailyFrom,
                DailyTo = req.DailyTo,
                // JEV: escopo resolvido no backend + diário sempre.
                IncludeDaily = true,
                ResolveScope = true,
                LookbackCandles = req.LookbackCandles,
                LookbackBubbles = req.LookbackBubbles,
            }, ct);

            try
            {
                // Mesmo contexto de níveis/cluster da strategy (paridade state).
                var candles = snap.Get<List<BarStorageItem>>(SnapshotLayers.Candles) ?? new();
                var asOf = candles.Count > 0
                    ? candles[^1].Date.AddMinutes(req.TimeFrame)
                    : displayDate.Date.AddDays(1);
                JevPrompt.EnsureLevelCluster(snap, asOf);

                var state = JevPrompt.BuildStateJson(req.Symbol, req.TimeFrame, snap);
                var stateJson = System.Text.Json.JsonSerializer.Serialize(state);
                var questions = JevPrompt.BuildQuestions();
                var request = _jev.BuildRequest(state, questions);
                var weights = await JevWeights.LoadAsync(_keeper);

                // Auditoria sempre (inclusive em erro): id gerado antes da chamada.
                var auditId = JevAuditFiles.NewId();
                JevService.JevResponse? resp = null;
                JevAnalysisResult? result = null;
                Exception? failure = null;
                try
                {
                    resp = await _jev.AskAsync(request, ct);
                    result = JevPrompt.ParseDecision(req.Symbol, resp.Model, resp.Body,
                        resp.InputTokens, resp.OutputTokens, stateJson, weights);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                var auditFile = await TryWriteAuditAsync(auditId, snap, stateJson,
                    request.Json, req, displayDate, resp, result, failure, ct);

                if (failure != null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo
                        .Capture(failure).Throw();

                result!.AuditFile = auditFile;
                _logger.LogInformation(
                    "[jev-debug] {Symbol} dia={Day} {Summary} (só log, sem ordem)",
                    result.Symbol, displayDate.ToString("yyyy-MM-dd"), JevPrompt.FormatCriteria(result));
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(503, ex.Message);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout Jev");
                return StatusCode(504, "Timeout chamando a Jev. Tente novamente.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Falha Jev");
                return StatusCode(502, ex.Message);
            }
        }

        /// <summary>
        /// Monta e grava a auditoria (best-effort). Nunca lança: falha de
        /// auditoria vira warning e não mascara o erro original da chamada.
        /// </summary>
        private async Task<string> TryWriteAuditAsync(
            string auditId,
            MarketSnapshot snap,
            string stateJson,
            string payload,
            JevAnalyzeRequest req,
            DateTime displayDate,
            JevService.JevResponse? resp,
            JevAnalysisResult? result,
            Exception? failure,
            CancellationToken ct)
        {
            try
            {
                var http = resp?.HttpStatus
                    ?? (failure as JevHttpException)?.HttpStatus ?? 0;
                var body = resp?.Body
                    ?? (failure as JevHttpException)?.Body ?? "";
                var attempts = resp?.Attempts
                    ?? (failure as JevHttpException)?.Attempts
                    ?? (failure as JevTimeoutException)?.Attempts ?? 1;
                var latency = resp?.LatencyMs
                    ?? (failure as JevHttpException)?.LatencyMs
                    ?? (failure as JevTimeoutException)?.LatencyMs ?? 0;

                var record = JevPrompt.BuildAudit(
                    auditId,
                    DateTime.Now, DateTime.Now, "debug",
                    req.Symbol, req.TimeFrame, displayDate,
                    "debug", resp?.Model ?? _jev.Model, "",
                    req.LookbackCandles, req.LookbackBubbles,
                    snap, stateJson,
                    payload,
                    http, latency, attempts, body, result,
                    JevPrompt.ParseErrorType(body), failure?.Message ?? "");
                var auditFile = await _audit.TryWriteAsync(record, ct);
                if (result != null) result.AuditFile = auditFile;
                return auditFile;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[jev-audit] falha ao montar auditoria {Symbol}", req.Symbol);
                return "";
            }
        }

        /// <summary>
        /// Lista as auditorias Jev do dia (1 arquivo por chamada em Data/JEV).
        /// GET api/Ai/Audit/{symbol}/{date}
        /// </summary>
        [HttpGet("{symbol}/{date}")]
        public IActionResult Audit(string symbol, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return BadRequest("Symbol é obrigatório (WINFUT/WDOFUT).");
            var sym = symbol.ToUpperInvariant();
            var dir = new DirectoryInfo(Path.Combine("Data", JevAuditFiles.Dir));
            if (!dir.Exists) return Ok(new List<object>());
            var prefix = $"{sym}_Jev_{date:yyyy-MM-dd}_";
            var out_ = new List<object>();
            foreach (var f in dir.GetFiles($"{sym}_Jev_*.json").Where(f => f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Name))
            {
                out_.Add(new Dictionary<string, object?> { ["file"] = f.Name, ["size"] = f.Length, ["modified"] = f.LastWriteTime });
            }
            return Ok(out_);
        }

        /// <summary>
        /// Devolve UMA auditoria completa (state + questions + resposta).
        /// GET api/Ai/AuditFile/{file}
        /// </summary>
        [HttpGet("{file}")]
        public async Task<IActionResult> AuditFile(string file)
        {
            var name = Path.GetFileName(file ?? "");
            if (string.IsNullOrWhiteSpace(name) || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Arquivo inválido.");
            if (name.Contains("..") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return BadRequest("Arquivo inválido.");
            var full = Path.Combine("Data", JevAuditFiles.Dir, name);
            if (!System.IO.File.Exists(full))
                return NotFound("Auditoria não encontrada.");
            var json = await System.IO.File.ReadAllTextAsync(full);
            return Content(json, "application/json");
        }
    }
}
