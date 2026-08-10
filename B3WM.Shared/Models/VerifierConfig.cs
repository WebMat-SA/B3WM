using B3WM.Shared.Models.Backtest;

namespace B3WM.Shared.Models
{
    public class VerifierConfig
    {
        public string Symbol { get; set; } = "WINFUT";
        public int TimeFrame { get; set; } = 2;
        public StrategyType StrategyName { get; set; } = StrategyType.SmartBreakout;
        public int Quantity { get; set; } = 1;
        public bool IsDayTrade { get; set; } = true;
        public string DayTradeCloseTime { get; set; } = "13:00";
        public int LookbackPeriod { get; set; } = 20;

        // estrutura (arquivos salvos / minDistance)
        public int MinDistance { get; set; } = 0;

        // critérios do SmartBreakout (thresholds de entrada/saída vêm dos
        // filtros de agentes da aba Bubbles, via BubbleThreshold/AgentThresholds)
        public double SmartVolumePct { get; set; } = Defaults.Backtest.SmartVolumePct;
        public double SmartStructureBufferPct { get; set; } = Defaults.Backtest.SmartStructureBufferPct;
        public List<int>? SmartAgents { get; set; }

        // filtro de bubbles (mesmo critério do gráfico: agentes + thresholds)
        public int BubbleThreshold { get; set; } = 0;
        public Dictionary<int, int>? AgentThresholds { get; set; }

        public BacktestConfig ToBacktestConfig() => new()
        {
            Symbol = Symbol,
            TimeFrame = TimeFrame,
            StrategyName = StrategyName,
            Quantity = Quantity,
            CommissionPerSide = 0,
            IsDayTrade = IsDayTrade,
            DayTradeCloseTime = DayTradeCloseTime,
            LookbackPeriod = LookbackPeriod,
            MinDistance = MinDistance,
            SmartVolumePct = SmartVolumePct,
            SmartStructureBufferPct = SmartStructureBufferPct,
            SmartAgents = SmartAgents,
            BubbleThreshold = BubbleThreshold,
            AgentThresholds = AgentThresholds
        };
    }
}
