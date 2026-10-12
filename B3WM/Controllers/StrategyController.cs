using B3WM.Services;
using B3WM.Services.Screen;
using B3WM.Services.Strategies;
using B3WM.Shared.Models;
using B3WM.Shared.Models.Strategies;
using Microsoft.AspNetCore.Mvc;

namespace B3WM.Controllers
{
    /// <summary>
    /// Aba Estratégia (issue #16, #22): lista strategies, arma/pausa/para sessões
    /// com snapshot da tela e expõe o log + relatório paper.
    /// Tudo log-only: envio real comentado no runner.
    /// PLAY é a ação única da seção Rodar estratégia (sem avaliação avulsa).
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class StrategyController : ControllerBase
    {
        private readonly StrategyRegistry _registry;
        private readonly StrategyRunner _runner;
        private readonly DataKeeperBase _keeper;
        private readonly ILogger<StrategyController> _logger;

        public StrategyController(
            StrategyRegistry registry,
            StrategyRunner runner,
            DataKeeperBase keeper,
            ILogger<StrategyController> logger)
        {
            _registry = registry;
            _runner = runner;
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
                DetailsMd = s.DetailsMd,
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
