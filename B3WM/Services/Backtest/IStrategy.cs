using B3WM.Shared.Models;

namespace B3WM.Services.Backtest
{
    public interface IStrategy
    {
        string Name { get; }
        string Description { get; }
        int WarmupBars { get; }
        Task InitializeAsync(CancellationToken ct = default);
        EntrySignal? TryGetEntry(BarStorageItem bar);
        ExitSignal? TryGetExit(BarStorageItem bar, BacktestPosition position);
        void Reset();
    }

    /// <summary>Estratégias que consomem bubbles ao vivo (ex.: SmartBreakout).</summary>
    public interface IBubbleConsumer
    {
        void OnBubble(BubbleStorageItem bubble);
    }

    /// <summary>Estratégias que expõem as linhas de estrutura desenhadas.</summary>
    public interface IStructureProvider
    {
        IReadOnlyList<StructureStorageItem> StructureLines { get; }
    }
}
