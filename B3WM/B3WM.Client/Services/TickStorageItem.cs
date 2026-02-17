using System.Text.Json.Serialization;
using B3WM.Shared.Entity;

namespace B3WM.Client.Services
{
    /// <summary>
    /// Ticks2 com "id" para keyPath do IndexedDB. Chave única: TrydID + Time (evita duplicatas).
    /// </summary>
    public class TickStorageItem : Ticks2
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        public static TickStorageItem FromTick(Ticks2 t)
        {
            return new TickStorageItem
            {
                Id = $"{t.TrydID}_{t.Time.Ticks}",
                TrydID = t.TrydID,
                Time = t.Time,
                Value = t.Value,
                Volume = t.Volume,
                Buyer = t.Buyer,
                Seller = t.Seller,
                Starter = t.Starter
            };
        }
    }
}
