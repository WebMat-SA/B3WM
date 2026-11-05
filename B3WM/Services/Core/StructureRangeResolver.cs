using B3WM.Shared.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace B3WM.Services.Core
{
    /// <summary>
    /// Deduz o último range de estrutura usado a partir dos próprios arquivos
    /// persistidos. No startup o backend não tem contato com o front, então usa
    /// o histórico para continuar no range que estava valendo em vez de voltar
    /// ao default.
    ///
    /// Regra: arquivos com histórico (primeiro registro anterior ao dia do
    /// arquivo) são válidos; artefatos de preload que só contêm o dia atual são
    /// ignorados. Em cada timeframe vence o range cujo último registro é mais
    /// recente (foi recalculado por último); depois há votação entre os
    /// timeframes intraday, o que evita que um artefato isolado (ex.: 250 no
    /// 60MIN) determine o range.
    /// </summary>
    public sealed class StructureRangeResolver
    {
        private const int DailyTimeFrame = 1440;
        private static readonly int[] IntradayTimeFrames = [1, 2, 5, 15, 30, 60];
        private static readonly TimeSpan CandidateWindow = TimeSpan.FromDays(14);

        private readonly string _dataDirectory;
        private readonly ILogger<StructureRangeResolver> _logger;
        private readonly ConcurrentDictionary<string, Lazy<Task<double?>>> _cache = new();

        public StructureRangeResolver(ILogger<StructureRangeResolver> logger)
            : this("Data", logger)
        {
        }

        internal StructureRangeResolver(string dataDirectory, ILogger<StructureRangeResolver> logger)
        {
            _dataDirectory = dataDirectory;
            _logger = logger;
        }

        public Task<double?> ResolveAsync(string symbol, bool daily)
        {
            var key = $"{symbol}|{(daily ? "D" : "I")}";
            var lazy = _cache.GetOrAdd(key,
                _ => new Lazy<Task<double?>>(() => ResolveCoreAsync(symbol, daily)));
            return lazy.Value;
        }

        private async Task<double?> ResolveCoreAsync(string symbol, bool daily)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol) || !Directory.Exists(_dataDirectory))
                    return null;

                int[] timeFrames = daily ? [DailyTimeFrame] : IntradayTimeFrames;
                var files = new List<FileCandidate>();
                DateTime newestFileDate = DateTime.MinValue;

                foreach (var path in Directory.EnumerateFiles(
                    _dataDirectory, $"{symbol}_StructureService_*MIN_*.json"))
                {
                    if (!TryParseFileName(Path.GetFileName(path), symbol,
                        out var timeFrame, out var distance, out var fileDate))
                        continue;
                    if (!timeFrames.Contains(timeFrame))
                        continue;
                    if (fileDate > DateTime.Today)
                        continue;

                    files.Add(new FileCandidate(path, timeFrame, distance, fileDate));
                    if (fileDate > newestFileDate)
                        newestFileDate = fileDate;
                }

                if (files.Count == 0)
                    return null;

                // Só considera os ranges recentes. A janela é relativa ao
                // arquivo mais novo (e não a hoje) para funcionar mesmo se o
                // servidor ficou dias parado.
                var cutoff = newestFileDate - CandidateWindow;
                files = files.Where(f => f.FileDate >= cutoff).ToList();
                if (files.Count == 0)
                    return null;

                // Um arquivo por (timeframe, range): o mais recente daquele range.
                var latestPerRange = new Dictionary<(int TimeFrame, double Distance), FileCandidate>();
                foreach (var file in files)
                {
                    var key = (file.TimeFrame, file.Distance);
                    if (!latestPerRange.TryGetValue(key, out var current)
                        || file.FileDate > current.FileDate)
                        latestPerRange[key] = file;
                }

                var byTimeFrame = new Dictionary<int, List<ResolvedCandidate>>();
                foreach (var file in latestPerRange.Values)
                {
                    var data = await ReadCandidateAsync(file);
                    if (data == null)
                        continue;

                    // Artefato de preload costuma só ter o dia do arquivo.
                    if (data.First.Date >= file.FileDate.Date)
                        continue;

                    if (!byTimeFrame.TryGetValue(file.TimeFrame, out var list))
                        byTimeFrame[file.TimeFrame] = list = [];

                    list.Add(new ResolvedCandidate(
                        file.Distance, data.First, data.Last, data.Count, file.FileDate));
                }

                if (byTimeFrame.Count == 0)
                    return null;

                var votes = new Dictionary<double, Vote>();
                foreach (var candidates in byTimeFrame.Values)
                {
                    var winner = candidates
                        .OrderByDescending(c => c.Last)
                        .ThenByDescending(c => c.Count)
                        .ThenByDescending(c => c.FileDate)
                        .First();

                    if (!votes.TryGetValue(winner.Distance, out var vote))
                        votes[winner.Distance] = vote = new Vote();

                    vote.TimeFrameWins++;
                    if (winner.Last > vote.Last)
                        vote.Last = winner.Last;
                    if (winner.FileDate > vote.FileDate)
                        vote.FileDate = winner.FileDate;
                    vote.Records += winner.Count;
                }

                var best = votes
                    .OrderByDescending(v => v.Value.TimeFrameWins)
                    .ThenByDescending(v => v.Value.Last)
                    .ThenByDescending(v => v.Value.Records)
                    .ThenByDescending(v => v.Value.FileDate)
                    .First();

                _logger.LogInformation(
                    "[structure-range] {Symbol} {Kind}: {Distance} (votos={Votes}, último={Last:yyyy-MM-dd HH:mm})",
                    symbol,
                    daily ? "1D" : "intraday",
                    best.Key,
                    best.Value.TimeFrameWins,
                    best.Value.Last);

                return best.Key;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[structure-range] Falha ao deduzir range de {Symbol}", symbol);
                return null;
            }
        }

        private static async Task<CandidateData?> ReadCandidateAsync(FileCandidate file)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file.Path);
                if (json.Contains('\0'))
                    return null;

                var items = JsonSerializer.Deserialize<List<StructureStorageItem>>(json);
                if (items is not { Count: > 0 })
                    return null;

                var first = items.Min(i => i.Date);
                var last = items.Max(i => i.Date);
                return new CandidateData(first, last, items.Count);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseFileName(
            string fileName, string symbol, out int timeFrame, out double distance, out DateTime fileDate)
        {
            timeFrame = 0;
            distance = 0;
            fileDate = default;

            var prefix = $"{symbol}_StructureService_";
            if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var rest = fileName[prefix.Length..];
            var minIndex = rest.IndexOf("MIN_", StringComparison.OrdinalIgnoreCase);
            if (minIndex <= 0)
                return false;

            if (!int.TryParse(rest[..minIndex], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out timeFrame))
                return false;

            var afterMin = rest[(minIndex + 4)..];
            if (!afterMin.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return false;

            var body = afterMin[..^5];
            var dateSeparator = body.LastIndexOf('_');
            if (dateSeparator <= 0)
                return false;

            var distanceText = body[..dateSeparator].Replace(',', '.');
            if (!double.TryParse(distanceText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out distance) || distance <= 0)
                return false;

            return DateTime.TryParseExact(body[(dateSeparator + 1)..], "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out fileDate);
        }

        private sealed record FileCandidate(string Path, int TimeFrame, double Distance, DateTime FileDate);

        private sealed record CandidateData(DateTime First, DateTime Last, int Count);

        private sealed record ResolvedCandidate(
            double Distance, DateTime First, DateTime Last, int Count, DateTime FileDate);

        private sealed class Vote
        {
            public int TimeFrameWins;
            public DateTime Last;
            public DateTime FileDate;
            public int Records;
        }
    }
}
