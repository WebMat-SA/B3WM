namespace B3WM.Shared.Models
{
    public class StructureStorageItem
    {
        public DateTime Date { get; set; }

        public string Symbol { get; set; }

        //InMinutes
        public int TimeFrame { get; set; }

        public double UpBorder { get; set; }

        public double DownBorder { get; set; }

        public double UpAuxBorder { get; set; }

        public double DownAuxBorder { get; set; }

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
