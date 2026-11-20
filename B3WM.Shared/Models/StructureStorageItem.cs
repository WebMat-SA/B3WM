using B3WM.Shared.Interfaces;

namespace B3WM.Shared.Models
{
    public class StructureStorageItem : ICloneable
    {
        public DateTime Date { get; set; }

        public string Symbol { get; set; }

        //InMinutes
        public int TimeFrame { get; set; }

        public double UpBorder { get; set; }

        public double DownBorder { get; set; }

        public double UpAuxBorder { get; set; }

        public double DownAuxBorder { get; set; }

        /// <summary>Modo do range usado neste print (manual | atr_leg | atr_bar).</summary>
        public string? RangeMode { get; set; }

        /// <summary>Range efetivo usado neste print (pontos). Null em arquivos antigos.</summary>
        public double? RangeUsed { get; set; }

        /// <summary>ATR no momento do print (pontos). Null em arquivos antigos.</summary>
        public double? Atr { get; set; }

        public object Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Json.JsonSerializer.Deserialize<StructureStorageItem>(json) ?? new StructureStorageItem();
        }

        public override string ToString()
        {
            return $"Date: {Date.ToString("dd/MM/yyyy HH:mm:ss")} | " +
                $"UpBorder:{UpBorder.ToString("00.00")} | " +
                $"UpAuxBorder:{UpAuxBorder.ToString("00.00")} | " +
                $"DownBorder:{DownBorder.ToString("00.00")} | " +
                $"DownAuxBorder:{DownAuxBorder.ToString("00.00")}";
        }
    }
}
