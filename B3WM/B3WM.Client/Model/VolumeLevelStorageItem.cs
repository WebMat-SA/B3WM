using System.Text.Json.Serialization;
using B3WM.Shared.Entity;

namespace B3WM.Client.Model
{
    /// <summary>
    /// VolumeLevel para IndexedDB com data da barra. KeyPath "id" (case-sensitive).
    /// BarDate permite filtrar volume por intervalo de datas ao remontar.
    /// </summary>
    public class VolumeLevelStorageItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("barDate")]
        public string BarDate { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public double Price { get; set; }

        [JsonPropertyName("total")]
        public long Total { get; set; }

        [JsonPropertyName("buyVolume")]
        public long BuyVolume { get; set; }

        [JsonPropertyName("sellVolume")]
        public long SellVolume { get; set; }

        /// <summary>Cria item a partir de VolumeLevel com data da barra (para salvar volume por período).</summary>
        public static VolumeLevelStorageItem FromVolumeLevel(VolumeLevel v, DateTime barDate)
        {
            var barDateStr = barDate.ToString("o");
            return new VolumeLevelStorageItem
            {
                Id = $"{barDateStr}_{v.Price.ToString("R")}",
                BarDate = barDateStr,
                Price = v.Price,
                Total = v.Total,
                BuyVolume = v.BuyVolume,
                SellVolume = v.SellVolume
            };
        }
    }
}
