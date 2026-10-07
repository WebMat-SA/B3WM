using B3WM.Shared.Entity;
using B3WM.Shared.Models;
using B3WM.Shared.Models.ExtremeDetection;

namespace B3WM.Services.Market
{
    /// <summary>
    /// Acesso genérico a dados de mercado de qualquer ativo/timeframe/data.
    /// Regra única live-vs-arquivo: data nula ou hoje → snapshot vivo
    /// (DataKeep); data passada → arquivos persistidos. É a porta de entrada
    /// para strategies novas (regra de negócio pura, sem saber de arquivos,
    /// serviços internos ou snapshots). Também serve replay/teste ao passar
    /// uma data histórica.
    /// </summary>
    public interface IMarketData
    {
        Task<List<BarStorageItem>> CandlesAsync(string symbol, int timeFrame, DateTime? date = null, int takeLast = int.MaxValue, CancellationToken ct = default);
        Task<List<BubbleStorageItem>> BubblesAsync(string symbol, DateTime? date = null, CancellationToken ct = default);
        Task<VolumeLevelStorageItem?> VolumeAsync(string symbol, DateTime? date = null, CancellationToken ct = default);
        Task<List<VolumeLevel>> VolumeLevelsAsync(string symbol, DateTime date, DateTime? from, DateTime? to, CancellationToken ct = default);
        Task<List<StructureStorageItem>> StructuresAsync(string symbol, int timeFrame, double minDistance, DateTime? date = null, CancellationToken ct = default);
        Task<List<StructureStorageItem>> DailyStructuresAsync(string symbol, double dailyDistance, DateTime date, int days = 90, CancellationToken ct = default);
        Task<ExtremeStorageItem?> IntradayExtremesAsync(string symbol, CancellationToken ct = default);
        Task<ExtremeStorageItem?> DailyExtremesAsync(string symbol, DateTime from, DateTime to, double noise, double prom, CancellationToken ct = default);
        Task<PivotStorageItem?> IntradayPivotAsync(string symbol, int lineCount, DateTime? date = null, CancellationToken ct = default);
        Task<PivotStorageItem?> DailyPivotAsync(string symbol, int lineCount, DateTime? date = null, CancellationToken ct = default);
    }
}
