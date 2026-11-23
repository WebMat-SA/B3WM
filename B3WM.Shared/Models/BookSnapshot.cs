namespace B3WM.Shared.Models
{
    /// <summary>
    /// Um nível de preço do livro de ofertas (Volume já agregado no preço).
    /// </summary>
    public sealed class BookLevel
    {
        public double Price { get; set; }

        public double Volume { get; set; }
    }

    /// <summary>
    /// Snapshot do livro recebido do ExtractorRTD via Profit LVL2 (RTD socket).
    /// O shape JSON casa com o OrderBook do bridge Python e com o modelo Dart.
    /// </summary>
    public sealed class BookSnapshot
    {
        public string Symbol { get; set; } = "";

        public DateTime Time { get; set; } = DateTime.UtcNow;

        public List<BookLevel> Bids { get; set; } = new();

        public List<BookLevel> Asks { get; set; } = new();

        public bool Available { get; set; } = true;

        public string Source { get; set; } = "rtd_lvl2";

        public string Message { get; set; } = "";
    }
}
