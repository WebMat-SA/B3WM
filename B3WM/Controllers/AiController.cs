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
        private readonly ILogger<AiController> _logger;

        public AiController(
            JevService jev,
            ScreenStateBuilder builder,
            ILogger<AiController> logger)
        {
            _jev = jev;
            _builder = builder;
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
                var resp = await _jev.AskAsync(state, JevPrompt.BuildQuestions(), ct);
                var result = JevPrompt.ParseDecision(req.Symbol, resp.Model, resp.Body,
                    resp.InputTokens, resp.OutputTokens, req.Threshold, state);
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
    }
}
