using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Screen;
using B3WM.Services.Strategies;
using B3WM.Shared.Models;
using B3WM.Shared.Models.AI;
using B3WM.Shared.Models.Strategies;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    /// <summary>
    /// Aba Estratégia (issue #16): lista strategies, arma/pausa/para sessões
    /// com snapshot da tela, avalia avulso e expõe o log + relatório paper.
    /// Tudo log-only: envio real comentado no runner.
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class StrategyController : ControllerBase
    {
        private readonly StrategyRegistry _registry;
        private readonly StrategyRunner _runner;
        private readonly ScreenStateBuilder _builder;
        private readonly DataKeeperBase _keeper;
        private readonly ILogger<StrategyController> _logger;

        public StrategyController(
            StrategyRegistry registry,
            StrategyRunner runner,
            ScreenStateBuilder builder,
            DataKeeperBase keeper,
            ILogger<StrategyController> logger)
        {
            _registry = registry;
            _runner = runner;
            _builder = builder;
            _keeper = keeper;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult List()
        {
            return Ok(_registry.All().Select(s => new StrategyInfo
            {
                Name = s.Name,
                Description = s.Description,
            }).ToList());
        }

        [HttpPost]
        public IActionResult Arm([FromBody] StrategyArmRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Strategy))
                return BadRequest("Strategy é obrigatória (ver List).");
            if (string.IsNullOrWhiteSpace(req.Symbol))
                return BadRequest("Symbol é obrigatório (WINFUT/WDOFUT).");
            IStrategy strategy;
            try { strategy = _registry.Get(req.Strategy); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }

            // Config 100% no código da strategy; o tab manda só strategy +
            // símbolo + foto da tela.
            var rawJson = req.ScreenConfig?.GetRawText() ?? "{}";
            var hash = StrategyRunner.SnapshotHashOf(rawJson);
            var filter = ReadBubbleFilter(req.ScreenConfig, req.Symbol);
            // TF da tela no PLAY (filtros travados na sessão, não muda).
            var tf = ScreenSpec.Int(ScreenSpecOf(req.ScreenConfig), "timeFrame", 2);
            if (tf == 1440) tf = 2;
            var id = _runner.Arm(strategy.Name, req.Symbol.ToUpperInvariant(),
                tf, rawJson, filter, hash);
            return Ok(new { sessionId = id, snapshotHash = hash });
        }

        private static System.Text.Json.JsonElement ScreenSpecOf(System.Text.Json.JsonElement? spec) =>
            spec is { ValueKind: System.Text.Json.JsonValueKind.Object } el ? el : default;

        /// <summary>Filtro de bolhas pré-extraído do blob (eficiente por evento).</summary>
        private static BubbleFilter ReadBubbleFilter(System.Text.Json.JsonElement? spec, string symbol)
        {
            if (spec is not { ValueKind: System.Text.Json.JsonValueKind.Object } el)
                return new BubbleFilter
                {
                    Threshold = Defaults.GetThresholdBubble(symbol),
                    AmountFilter = true, AgentsFilter = false,
                };
            return new BubbleFilter
            {
                Threshold = ScreenSpec.Int(el, "thresholdBubble", Defaults.GetThresholdBubble(symbol)),
                PerAgent = ScreenSpec.IntMap(el, "agentThresholds"),
                Agents = ScreenSpec.IntSet(el, "selectedAgents"),
                AmountFilter = ScreenSpec.Bool(el, "bubbleAmountFilter", true),
                AgentsFilter = ScreenSpec.Bool(el, "bubbleAgentsFilter", true),
            };
        }

        [HttpPost]
        public IActionResult Pause([FromQuery] string sessionId, [FromQuery] bool paused = true)
        {
            return _runner.Pause(sessionId, paused)
                ? Ok(new { sessionId, paused })
                : NotFound("Sessão não encontrada.");
        }

        [HttpPost]
        public IActionResult Stop([FromQuery] string sessionId)
        {
            var log = _runner.Stop(sessionId);
            return log == null ? NotFound("Sessão não encontrada.") : Ok(log);
        }

        [HttpGet]
        public IActionResult State() => Ok(_runner.States());

        /// <summary>Avaliação avulsa (botão "avaliar agora"): sem sessão, só log.</summary>
        [HttpPost]
        public async Task<IActionResult> Evaluate([FromBody] StrategyArmRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Strategy))
                return BadRequest("Strategy é obrigatória (ver List).");
            IStrategy strategy;
            try { strategy = _registry.Get(req.Strategy); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }

            var displayDate = (req.DisplayDate?.Date ?? DateTime.Today);
            var specRaw = req.ScreenConfig?.GetRawText() ?? "{}";
            using var specDoc = System.Text.Json.JsonDocument.Parse(specRaw);
            var tf = ScreenSpec.Int(specDoc.RootElement, "timeFrame", 2);
            if (tf == 1440) tf = 2;
            // Só o último candle (evento); a strategy resolve o snapshot
            // completo com os próprios consts.
            var state = await _builder.BuildAsync(new ScreenStateRequest
            {
                Symbol = req.Symbol,
                TimeFrame = tf,
                DisplayDate = displayDate,
                VisibleTimeFrames = new() { tf },
                Spec = specDoc.RootElement.Clone(),
                IncludeDaily = ScreenSpec.Bool(ScreenSpec.Daily(specDoc.RootElement), "panelVisible", false),
                LookbackCandles = 1,
                LookbackBubbles = 0,
            }, ct);

            var hash = StrategyRunner.SnapshotHashOf(specRaw);
            var candles = state.Get<List<Shared.Models.BarStorageItem>>(SnapshotLayers.Candles) ?? new();
            StrategyDecision? decision;
            try
            {
                using var scope = HttpContext.RequestServices.CreateScope();
                var scoped = scope.ServiceProvider.GetServices<IStrategy>()
                    .First(s => string.Equals(s.Name, strategy.Name, StringComparison.OrdinalIgnoreCase));
                decision = await scoped.EvaluateAsync(
                    new CandleClosed
                    {
                        Symbol = req.Symbol,
                        At = DateTime.Now,
                        Bar = candles.Count > 0 ? candles[^1] : new(),
                    },
                    new StrategyContext
                    {
                        Symbol = req.Symbol, ScreenSpecJson = specRaw,
                        SnapshotHash = hash, Snapshot = state,
                    }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no Evaluate avulso");
                return StatusCode(502, "Falha ao avaliar: " + ex.Message);
            }
            _logger.LogInformation("[strategy:{Name}:avulso] side={Side} conf={Conf:F2} snap={Hash} (só log)",
                strategy.Name, decision?.Side, decision?.Confidence ?? 0, hash);
            return Ok(decision);
        }

        [HttpGet("{symbol}/{date}")]
        public async Task<IActionResult> Log(string symbol, DateTime date)
        {
            // Lê todos os logs de strategy do dia (um arquivo por strategy).
            var prefix = $"{symbol}_Strategy_";
            var suffix = $"_{date:yyyy-MM-dd}.json";
            var dir = new DirectoryInfo("Data");
            if (!dir.Exists) return Ok(new List<StrategyLogDay>());
            var out_ = new List<StrategyLogDay>();
            foreach (var f in dir.GetFiles($"{prefix}*{suffix}"))
            {
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(f.FullName);
                    var log = System.Text.Json.JsonSerializer.Deserialize<StrategyLogDay>(json);
                    if (log != null) out_.Add(log);
                }
                catch (FileNotFoundException) { /* sumiu entre listar e ler: ignora */ }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha lendo log de strategy {File}", f.Name);
                }
            }
            return Ok(out_.OrderBy(l => l.Strategy).ToList());
        }
    }
}
