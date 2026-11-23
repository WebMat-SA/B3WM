using System.Collections.Concurrent;
using B3WM.Shared.Models;

namespace B3WM.Services;

/// <summary>
/// Guarda o último livro LVL2 recebido do ExtractorRTD por símbolo. O
/// TradeController usa esse snapshot quando está fresco e cai para o MT5
/// (bridge Python) caso contrário.
/// </summary>
public sealed class BookStore
{
    private const int MaxLevels = 30;

    private readonly ConcurrentDictionary<string, BookSnapshot> _books =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, DateTime> _receivedAt =
        new(StringComparer.OrdinalIgnoreCase);

    public void Update(BookSnapshot snapshot)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Symbol))
            return;

        var symbol = snapshot.Symbol.Trim().ToUpperInvariant();
        var normalized = new BookSnapshot
        {
            Symbol = symbol,
            Time = DateTime.UtcNow,
            Bids = Normalize(snapshot.Bids, descending: true),
            Asks = Normalize(snapshot.Asks, descending: false),
            Available = snapshot.Available,
            Source = string.IsNullOrWhiteSpace(snapshot.Source)
                ? "rtd_lvl2"
                : snapshot.Source,
            Message = snapshot.Message ?? "",
        };

        _books[symbol] = normalized;
        _receivedAt[symbol] = DateTime.UtcNow;
    }

    public bool TryGet(string symbol, TimeSpan maxAge, out BookSnapshot snapshot)
    {
        snapshot = null!;
        if (string.IsNullOrWhiteSpace(symbol))
            return false;

        var key = symbol.Trim().ToUpperInvariant();
        if (!_books.TryGetValue(key, out var book))
            return false;
        if (!_receivedAt.TryGetValue(key, out var received))
            return false;
        if (DateTime.UtcNow - received > maxAge)
            return false;
        if (!book.Available)
            return false;

        snapshot = book;
        return true;
    }

    private static List<BookLevel> Normalize(
        IEnumerable<BookLevel>? levels, bool descending)
    {
        if (levels is null)
            return new List<BookLevel>();

        return levels
            .Where(l => l is not null && l.Price > 0 && l.Volume > 0)
            .GroupBy(l => Math.Round(l.Price, 6))
            .Select(g => new BookLevel
            {
                Price = g.Key,
                Volume = g.Sum(x => x.Volume),
            })
            .OrderBy(l => descending ? -l.Price : l.Price)
            .Take(MaxLevels)
            .ToList();
    }
}
