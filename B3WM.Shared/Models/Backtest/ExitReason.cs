namespace B3WM.Shared.Models.Backtest
{
    public enum ExitReason
    {
        StopLoss,
        TakeProfit,
        Reversal,
        StrategySignal,
        EndOfData,
        DayTradeClose
    }
}
