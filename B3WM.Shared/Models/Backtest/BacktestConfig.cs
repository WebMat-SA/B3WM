using B3WM.Shared.Models;

namespace B3WM.Shared.Models.Backtest
{
    public class BacktestConfig
    {
        public string Symbol { get; set; } = "WINFUT";
        public int TimeFrame { get; set; } = 2;
        public StrategyType StrategyName { get; set; } = StrategyType.SmartBreakout;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double StopLossPoints { get; set; } = 200;
        public double TakeProfitPoints { get; set; } = 400;
        public int Quantity { get; set; } = 1;
        public double CommissionPerSide { get; set; } = 0.90;
        public int LookbackPeriod { get; set; } = 20;
        public bool IsDayTrade { get; set; } = true;
        public string DayTradeCloseTime { get; set; } = "13:00";

        // estrutura (usado para carregar os arquivos salvos do StructureService)
        public int MinDistance { get; set; } = 0;

        // critérios do SmartBreakout
        public double SmartEntryThreshold { get; set; } = Defaults.Backtest.SmartEntryThreshold;
        public double SmartExitThreshold { get; set; } = Defaults.Backtest.SmartExitThreshold;
        public double SmartVolumePct { get; set; } = Defaults.Backtest.SmartVolumePct;
        public double SmartStructureBufferPct { get; set; } = Defaults.Backtest.SmartStructureBufferPct;
        public List<int>? SmartAgents { get; set; }

        // filtro de bubbles (mesmos critérios da aba Bubbles do gráfico)
        public int BubbleThreshold { get; set; } = 0;
        public Dictionary<int, int>? AgentThresholds { get; set; }
    }
}
