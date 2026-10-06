using B3WM.Shared.Models;

namespace B3WM.Services.Strategies
{
    /// <summary>
    /// Filtro de bolhas da tela, pré-extraído do blob no Arm (eficiente por
    /// evento; sem re-parse do JSON, sem espelho tipado).
    /// </summary>
    public sealed class BubbleFilter
    {
        public bool Enabled { get; set; } = true;
        public int Threshold { get; set; }
        public Dictionary<int, int> PerAgent { get; set; } = new();
        public HashSet<int> Agents { get; set; } = new();
        public bool AmountFilter { get; set; } = true;
        public bool AgentsFilter { get; set; } = true;

        public bool Passes(BubbleStorageItem b) =>
            Enabled && AI.JevPrompt.BubblePasses(b, Threshold, PerAgent, Agents, AmountFilter, AgentsFilter);
    }

    /// <summary>
    /// Contexto da avaliação do gatilho (leve, sem I/O): quem decide SE avalia
    /// é o `IStrategy.ShouldTrigger` de cada strategy. TimeFrame = TF da tela
    /// no PLAY (filtros travados na sessão, então não muda).
    /// </summary>
    public sealed class StrategyTriggerContext
    {
        public string Symbol { get; set; } = "";
        public int TimeFrame { get; set; } = 2;
        public BubbleFilter? Filter { get; set; }
    }

    /// <summary>
    /// Predicados reutilizáveis para `ShouldTrigger` (blocos de montar das
    /// condições, com a decisão final sempre na strategy).
    /// </summary>
    public static class TriggerChecks
    {
        public static bool IsCandleClose(StrategyEvent ev, int timeFrame) =>
            ev is CandleClosed c && c.Bar.TimeFrame == timeFrame;

        public static bool IsVisibleBubble(StrategyEvent ev, BubbleFilter? filter)
        {
            if (ev is not BubbleAppeared b)
                return false;
            return filter == null || filter.Passes(b.Item);
        }
    }
}
