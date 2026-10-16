using B3WM.Services.AI;
using B3WM.Services.Screen;
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
        private readonly ILogger<AiController> _logger;

        public AiController(
            JevService jev,
            ScreenStateBuilder builder,
            JevAuditStore audit,
            ILogger<AiController> logger)
        {
            _jev = jev;
            _builder = builder;
            _audit = audit;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Status()
        {
            // Nunca vazar o valor da chave — só se existe.
            return Ok(new { enabled = true, hasKey = _jev.HasKey, model = _jev.Model });
        }

        [HttpPost]
        public async Task<IActionResult> Analyze([FromBody] JevAnalyzeRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Symbol))
                return BadRequest("Symbol é obrigatório (WINFUT/WDOFUT).");
            req.TimeFrame = req.TimeFrame <= 0 ? 5 : req.TimeFrame;
            req.LookbackCandles = Math.Clamp(req.LookbackCandles, 5, 200);
            req.LookbackBubbles = Math.Clamp(req.LookbackBubbles, 0, 200);
            req.Threshold = Math.Clamp(req.Threshold, 0, 1);

            if (!_jev.HasKey)
                return StatusCode(503, "JevKey ausente no servidor. Configure via 'dotnet user-secrets set JevKey <sua-chave>'.");

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
                IncludeDaily = ScreenSpec.Bool(daily, "panelVisible", false),
                LookbackCandles = req.LookbackCandles,
                LookbackBubbles = req.LookbackBubbles,
            }, ct);

            try
            {
                var state = JevPrompt.BuildState(req.Symbol, req.TimeFrame, snap);
                var questions = JevPrompt.BuildQuestions();
                var resp = await _jev.AskAsync(state, questions, ct);
                var result = JevPrompt.ParseDecision(req.Symbol, resp.Model, resp.Body,
                    resp.InputTokens, resp.OutputTokens, req.Threshold, state);
                // Auditoria best-effort: 1 arquivo por chamada em Data/JEV.
                try
                {
                    var record = JevPrompt.BuildAudit(
                        JevAuditFiles.NewId(),
                        DateTime.Now, DateTime.Now, "debug",
                        req.Symbol, req.TimeFrame, displayDate,
                        "debug", resp.Model, "",
                        req.Threshold, req.LookbackCandles, req.LookbackBubbles,
                        snap, state,
                        System.Text.Json.JsonSerializer.Serialize(questions),
                        resp.HttpStatus, resp.LatencyMs, resp.Attempts,
                        resp.Body, result);
                    result.AuditFile = await _audit.TryWriteAsync(record, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[jev-audit] falha ao montar auditoria {Symbol}", req.Symbol);
                }
                _logger.LogInformation(
                    "[jev-debug] {Symbol} dia={Day} direcao={D} conf={C:F2} shouldTrade={T} (só log, sem ordem)",
                    result.Symbol, displayDate.ToString("yyyy-MM-dd"),
                    result.Direcao, result.DirecaoConfidence, result.ShouldTrade);
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
                out_.Add(new { file = f.Name, size = f.Length, modified = f.LastWriteTime });
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
