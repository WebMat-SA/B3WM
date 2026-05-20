using B3WM.Shared.Entity;

namespace B3WM.Shared.Models
{
    /// <summary>
    /// Barras para IndexedDB com timeframe. KeyPath "id" (case-sensitive).
    /// </summary>
    public class BarStorageItem
    {
        public virtual DateTime Date { get; set; }

        public virtual string Symbol { get; set; }

        //InMinutes
        public virtual int TimeFrame { get; set; }

        public virtual double Open { get; set; }

        public virtual double High { get; set; }

        public virtual double Low { get; set; }

        public virtual double Close { get; set; }

        public virtual long Volume { get; set; }

        public virtual List<VolumeLevel>? VolumeLevel { get; set; }

        public override string ToString()
        {
            return $"Date: {Date}, Symbol: {Symbol}, TimeFrame: {TimeFrame}, Open: {Open}, High: {High}, Low: {Low}, Close: {Close}";
        }
    }
}
