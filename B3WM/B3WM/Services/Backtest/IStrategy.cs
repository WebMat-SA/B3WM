using B3WM.Shared.Models;

namespace B3WM.Services.Backtest
{
    public interface IStrategy
    {
        string Name { get; }
        Task InitializeAsync();
        Signal? Evaluate(BarStorageItem bar, bool hasPosition);
        void Reset();
    }
}
