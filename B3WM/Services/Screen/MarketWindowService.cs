using System.Collections.Concurrent;
using B3WM.Services.Market;
using B3WM.Shared.Models;

namespace B3WM.Services.Screen
{
    /// <summary>
    /// Fonte única da janela do automode por estrutura (intraday + diário) no
    /// backend. Resolve via <see cref="StructureWindowResolver"/> e serve tanto
    /// o REST (GetMarketWindow) quanto o push SignalR (ReceiveOnMarketWindow).
    /// Singleton: cria um scope por chamada para o IMarketData (scoped) e
    /// mantém um cache da JANELA DIÁRIA por símbolo, invalidado quando chega
    /// uma estrutura 1440 nova (chave = última data 1440 no histórico).
    /// </summary>
    public sealed class MarketWindowService
    {
        private const int DailyTimeFrame = 1440;

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<MarketWindowService> _logger;
        private readonly ConcurrentDictionary<string, DailyCacheEntry> _dailyCache = new();

        public MarketWindowService(IServiceScopeFactory scopes, ILogger<MarketWindowService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        private sealed record DailyCacheEntry(StructureWindow Window, DateTime LastStructure);

        public void InvalidateDaily(string symbol) => _dailyCache.TryRemove(symbol, out _);

        public async Task<MarketWindow> ResolveAsync(MarketWindowRequest req, CancellationToken ct = default)
        {
            using var scope = _scopes.CreateScope();
            var market = scope.ServiceProvider.GetRequiredService<IMarketData>();

            var symbol = req.Symbol;
            var tf = Defaults.TimeFrames.Contains(req.TimeFrame) ? req.TimeFrame : 5;
            var today = DateTime.Today;

            var intraday = await ResolveIntradayAsync(market, req, symbol, tf, today, ct);
            var daily = await ResolveDailyAsync(market, req, symbol, today, ct);

            return new MarketWindow
            {
                Symbol = symbol,
                TimeFrame = tf,
                Intraday = intraday,
                Daily = daily,
            };
        }

        private static async Task<StructureWindow> ResolveIntradayAsync(
            IMarketData market, MarketWindowRequest req, string symbol, int tf, DateTime today, CancellationToken ct)
        {
            if (!req.ProfileAuto && (req.ProfileFrom != null || req.ProfileTo != null))
            {
                return new StructureWindow
                {
                    Auto = false,
                    From = (req.ProfileFrom ?? today).Date,
                    To = (req.ProfileTo ?? today).Date,
                };
            }

            var (bars, structures) = await LoadRangeAsync(market, symbol, tf, req, today, ct);
            var win = StructureWindowResolver.ResolveIntraday(
                bars, structures, symbol, tf, Defaults.GetTickSize(symbol));
            if (win is { } w)
                return new StructureWindow { Auto = true, From = w.From, To = w.To };
            return new StructureWindow
            {
                Auto = true,
                From = bars.Count > 0 ? bars[0].Date : today,
                To = bars.Count > 0 ? bars[^1].Date : today,
            };
        }

        /// <summary>
        /// Carrega barras + estruturas do timeframe no range da tela (multi-dia)
        /// ou só no dia de hoje (intraday), para o automode usar as MESMAS barras
        /// que o app. Estruturas por dia (fonte única por candle fechado).
        /// </summary>
        private static async Task<(List<BarStorageItem> Bars, List<StructureStorageItem> Structures)> LoadRangeAsync(
            IMarketData market, string symbol, int tf, MarketWindowRequest req, DateTime today, CancellationToken ct)
        {
            var minDistance = Defaults.GetMinDistance(symbol);
            var start = (req.RangeFrom ?? today).Date;
            var end = (req.RangeTo ?? today).Date;
            if (end < start) (start, end) = (end, start);
            if (end > today) end = today;
            if ((end - start).TotalDays > 400) start = end.AddDays(-400);

            var bars = new List<BarStorageItem>();
            var structures = new List<StructureStorageItem>();
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                bars.AddRange(await market.CandlesAsync(symbol, tf, day, int.MaxValue, ct));
                structures.AddRange(await market.StructuresAsync(symbol, tf, minDistance, day, ct));
            }
            bars.Sort((a, b) => a.Date.CompareTo(b.Date));
            structures.Sort((a, b) => a.Date.CompareTo(b.Date));
            return (bars, structures);
        }

        private async Task<StructureWindow> ResolveDailyAsync(
            IMarketData market, MarketWindowRequest req, string symbol, DateTime today, CancellationToken ct)
        {
            if (!req.DailyAuto && (req.DailyFrom != null || req.DailyTo != null))
            {
                return new StructureWindow
                {
                    Auto = false,
                    From = (req.DailyFrom ?? today).Date,
                    To = (req.DailyTo ?? today).Date,
                };
            }

            var dailyStructs = await market.DailyStructuresAsync(
                symbol, Defaults.GetMinDistanceDaily(symbol), today, 365, ct);
            var lastStruct = dailyStructs.Count > 0
                ? dailyStructs.Max(s => s.Date)
                : DateTime.MinValue;

            // Cache: janela diária só muda quando muda o último print 1440.
            if (_dailyCache.TryGetValue(symbol, out var cached) && cached.LastStructure == lastStruct)
                return cached.Window;

            var bars = await market.DailyCandlesAsync(symbol, today.AddDays(-365), today, ct);
            var (from, to) = StructureWindowResolver.ResolveDaily(bars, dailyStructs, symbol, today);
            var window = new StructureWindow { Auto = true, From = from, To = to };
            _dailyCache[symbol] = new DailyCacheEntry(window, lastStruct);
            _logger.LogDebug("[market-window] daily resolvida {Symbol} {From:yyyy-MM-dd}..{To:yyyy-MM-dd} (prints 1440={N})",
                symbol, from, to, dailyStructs.Count);
            return window;
        }
    }
}
