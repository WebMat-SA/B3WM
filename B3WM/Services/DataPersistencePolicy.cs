using B3WM.Shared.Models;
using B3WM.Shared.Models.Strategies;

namespace B3WM.Services
{
    /// <summary>
    /// Política central de persistência: só grava arquivos quando a data
    /// corresponde ao pregão atual e existe dado real da sessão. A ausência de
    /// ticks/candles é o sinal de dia sem pregão (não há calendário B3 aqui).
    /// </summary>
    public static class DataPersistencePolicy
    {
        public static bool IsToday(DateTime value, DateTime today) =>
            value.Date == today.Date;

        public static bool ShouldUseInMemoryToday(DateTime requested, DateTime today) =>
            IsToday(requested, today);

        public static bool ShouldPersistVolume(VolumeLevelStorageItem? snapshot, DateTime today) =>
            snapshot != null
            && IsToday(snapshot.Date, today)
            && snapshot.Volumes is { Count: > 0 };

        public static bool ShouldPersistForecast(AdjustmentForecastItem? forecast, DateTime today) =>
            forecast != null
            && forecast.SumV > 0
            && IsToday(forecast.Day != default ? forecast.Day : forecast.Time, today);

        public static bool ShouldPersistExtremes(
            ExtremeStorageItem? snapshot, DateTime today, bool hasFreshLiveProfile) =>
            hasFreshLiveProfile
            && snapshot != null
            && IsToday(snapshot.Date, today)
            && snapshot.Statistics?.PointCount > 0;

        public static bool ShouldPersistStructure(BarStorageItem? bar, DateTime today) =>
            bar != null
            && IsToday(bar.Date, today);

        public static bool ShouldPersistStrategyLog(StrategyLogDay? log) =>
            log != null
            && (log.Decisions.Count > 0 || log.PaperTrades.Count > 0);
    }
}
