using System.Text.Json.Serialization;
using B3WM.Shared.Entity;

namespace B3WM.Client.Model
{
    /// <summary>
    /// Barras para IndexedDB com timeframe. KeyPath "id" (case-sensitive).
    /// </summary>
    public class BarStorageItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("open")]
        public double Open { get; set; }

        [JsonPropertyName("high")]
        public double High { get; set; }

        [JsonPropertyName("low")]
        public double Low { get; set; }

        [JsonPropertyName("close")]
        public double Close { get; set; }

        [JsonPropertyName("volume")]
        public long Volume { get; set; }

        [JsonPropertyName("timeframe")]
        public int Timeframe { get; set; }

        public static BarStorageItem FromBar(Bars bar, int timeframe)
        {
            return new BarStorageItem
            {
                Id = $"{timeframe}_{bar.Date.Ticks}",
                Date = bar.Date.ToString("o"),
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume,
                Timeframe = timeframe
            };
        }
    }
}
