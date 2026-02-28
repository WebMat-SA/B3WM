using System.Text.Json.Serialization;
using B3WM.Shared.Entity;

namespace B3WM.Client.Services
{
    /// <summary>
    /// Bubbles para IndexedDB. KeyPath "id" (case-sensitive). Sem timeframe.
    /// </summary>
    public class BubbleStorageItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public double Price { get; set; }

        [JsonPropertyName("agent")]
        public int? Agent { get; set; }

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("time")]
        public string Time { get; set; } = string.Empty;

        [JsonPropertyName("actionType")]
        public int? ActionType { get; set; }

        public static BubbleStorageItem FromBubble(Bubble b, int index)
        {
            return new BubbleStorageItem
            {
                Id = $"{b.Time.Ticks}_{(int?)b.Agent}_{b.Price}_{b.Amount}_{(int?)b.ActionType}_{index}",
                Price = b.Price,
                Agent = b.Agent.HasValue ? (int)b.Agent.Value : null,
                Amount = b.Amount,
                Time = b.Time.ToString("o"),
                ActionType = b.ActionType.HasValue ? (int)b.ActionType.Value : null
            };
        }
    }
}
