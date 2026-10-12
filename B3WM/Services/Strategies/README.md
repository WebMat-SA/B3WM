# Strategies — receita de strategy nova

Tudo que roda no backend mora em `B3WM/Services/Strategies/`.
A aba Estratégia do app lista e arma sem mudar nada no Flutter.

## Camadas (não misturar)

- `Services/Screen/` — **foto da tela** (uso geral, não é de IA):
  `ScreenSpec` (leitura do blob + chaves Handled/Cosmetic),
  `ScreenStateBuilder` (+`ScreenStateRequest`) que monta o `MarketSnapshot`
  sobre o `IMarketData`. Filtro novo no app = classifica a chave e lê via
  `Spec*` aqui.
- `Services/Market/` — **dados** (`IMarketData`): qualquer ativo/TF/data,
  live ou arquivo. Porta de entrada das strategies rule-based.
- `Services/AI/` — **só Jev**: `JevService` (transporte HTTP: autentica,
  envia, retry 429/529, devolve JSON cru via `AskAsync`) + `JevPrompt`
  (contrato de prompt: `BuildState`, `BuildQuestions`, `ParseDecision`,
  filtro de bolhas, VWAP — puro, sem I/O, testado sem chave).
- `Services/Strategies/` — **decisão e execução**: `IStrategy`
  (`ShouldTrigger` + `EvaluateAsync`), `StrategyRunner` (gatilhos, paper,
  `WOULD-SEND`), `StrategyController` (aba).

## Nova strategy em 5 passos

1. **Classe**: `public sealed class MinhaStrategy : StrategyBase`
   com ctor `(ILogger<MinhaStrategy> logger, ...deps...) : base(logger)`.
   - Regra simples: injete **só `IMarketData`**
     (`CandlesAsync/BubblesAsync/VolumeAsync/VolumeLevelsAsync/StructuresAsync/DailyStructuresAsync/IntradayExtremesAsync/DailyExtremesAsync/IntradayPivotAsync/DailyPivotAsync`
     com `(symbol, timeframe?, date?)` — qualquer ativo/TF, live ou arquivo).
   - Regra com IA: injete `ScreenStateBuilder` + `JevService`
     (ver `JevAnalysisStrategy`: monta snapshot, `JevPrompt.BuildState` +
     `AskAsync` + `JevPrompt.ParseDecision`, `ApplyPosition` da base).
2. **Quando dispara**: implemente `ShouldTrigger(ev, ctx)` — **síncrono,
   barato, sem I/O**, lendo só o evento + `ctx{Symbol, TimeFrame, Filter}`.
   `TimeFrame` = TF da tela no PLAY (filtros travados, não muda).
   Blocos prontos em `TriggerChecks`: `IsCandleClose(ev, tf)`,
   `IsVisibleBubble(ev, filter)` — combine como quiser.
   Ex. bubble (1ª visível por bucket do TF, ritmo no runner):
   `TriggerChecks.IsVisibleBubble(ev, ctx.Filter)`.
   Ex. candle: `ev is CandleClosed c && c.Bar.TimeFrame == ctx.TimeFrame`.
3. **Metadados + config no código**: `Name` (único, aparece no dropdown),
   `Description` (1 linha sob o dropdown) e `DetailsMd` (obrigatório, Markdown
   renderizado sob o dropdown: o que faz, gatilho, TF, parâmetros fixos no
   código, riscos/limitações) + `private const` para thresholds/lookbacks —
   **nada de config vai para a aba** (só dropdown, PLAY e sessões).
4. **Regra**: `EvaluateAsync(StrategyEvent ev, StrategyContext ctx, ct)`
   retorna `StrategyDecision { Side, Confidence, Encerrar, ShouldTrade, Reason }`
   ou `null`. `ctx.Position` traz a posição paper (null = flat).
   **Nunca opera aqui**: o runner decide paper e o envio real está comentado nele.
5. **DI**: 2 linhas em `B3WM/Extensions.cs`:
   `services.AddTransient<MinhaStrategy>();`
   `services.AddTransient<IStrategy, MinhaStrategy>(sp => sp.GetRequiredService<MinhaStrategy>());`
6. **Validação**: aparece em `GET api/Strategy/List`; teste com `IMarketData`
   mockado (ver `SampleRuleStrategyTests`).

## Filtros da tela

O app envia o `SymbolConfig.toJson()` opaco em todo Arm. Leituras no
backend usam `ScreenSpec.Int/Num/Bool/Str/IntMap/IntSet` com os defaults do
Dart — **sem espelho tipado**. Filtro novo no Flutter: classifica a chave em
`ScreenSpecKeys.Handled` (lida em algum lugar) ou `.Cosmetic` (só estética);
o teste `ScreenSpecKeysTest` falha até isso estar feito.

## Gatilhos e execução (runner)

Cada strategy decide no próprio `ShouldTrigger(ev, tctx{Symbol, TimeFrame,
Filter})` — TF = TF da tela no PLAY. Fonte dos fatos: `OrchestratorService`
por símbolo (candle enriquecido + bubble repassada); o runner é genérico
multi-trigger: repassa todo evento do símbolo (`candle_close`, `bubble`,
futuros via `PublishAsync`) e cada strategy filtra no `ShouldTrigger` (ex.:
`JevAnalysis` só `IsVisibleBubble` — 1ª por bucket do TF, ritmo no runner;
outra pode ser só `IsCandleClose`). Disparou →
`EvaluateAsync` com `StrategyContext{Symbol, TimeFrame, ...}` → decisão logada (`[strategy:Nome:sessão] ...`) → paper
engine genérica (abre no gate, fecha em `encerrar > 0.5`, executa
no open seguinte, zera na virada do dia) → `WOULD-SEND` no log.
Candle fechado além de evento também move execução/persistência.
Futuro (ex.: toque em pivot): criar o `StrategyEvent` novo e despachar via
`StrategyRunner.PublishAsync(ev, "motivo")`, sem mudar o runner.
Relatório paper em `Data/{SYM}_Strategy_{Nome}_{data}.json`.
Cada avaliação e cada execução gera um `StrategyDecisionLog`
(Kind avaliacao/execucao, hora, evento, lado, confiança, ação);
todos os itens do dia voltam no `State` (`ReportItems`, mais recentes primeiro) e aparecem
sob a sessão aberta na aba Estratégia.
