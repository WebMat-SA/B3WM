using B3WM.Shared.Entity;
using Magic.IndexedDb.SchemaAnnotations;
using Nextended.Core.Extensions;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace B3WM.Client.Model
{
    /// <summary>
    /// VolumeLevel para IndexedDB com data da barra. KeyPath "id" (case-sensitive).
    /// BarDate permite filtrar volume por intervalo de datas ao remontar.
    /// </summary>
    public class VolumeLevelStorageItem
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public string Symbol { get; set; }

        //InMinutes
        public int TimeFrame { get; set; }

        public List<VolumeLevel> Volumes { get; set; } = new();

        public static List<VolumeLevel> FromTo(List<BarStorageItem> data, DateTime from, DateTime to)
        {
            List<VolumeLevel> result = new List<VolumeLevel>();

            //os volumes são somatorias do intraday

            //barstorageitem do datetime to + ultimo bastorageitem do dia anterior - bastorageitem from

            var barStorageTo = data.FirstOrDefault(q => q.Date == to);
            var barStorageFrom = data.FirstOrDefault(q => q.Date == from);

            if (barStorageTo == null || barStorageFrom == null) return new();

            result = barStorageTo.VolumeLevel ?? new();

            var grouping = data.GroupBy(q => q.Date.Date);

            //sum last register of volumes
            foreach(var item in grouping.Where(q=>q.Key >= from.Date && q.Key < to.Date).OrderByDescending(q=>q.Key))
            {
                //get last voluems
                var volumeLevel = item.OrderByDescending(q=>q.Date).First().VolumeLevel;

                result = Operation(result, volumeLevel ?? new(), "Sum");
            }

            result = Operation(result, barStorageFrom.VolumeLevel ?? new(), "Diff");


            return result;
        }

        private static List<VolumeLevel> Operation(List<VolumeLevel> vol1, List<VolumeLevel> vol2, string Operation)
        {
            ConcurrentDictionary<double, VolumeLevel> dictionary = new ConcurrentDictionary<double, VolumeLevel>();

            foreach(var item in vol1)
            {
                dictionary.AddOrUpdate(item.Price, item,
                    (price, exisiting) =>
                    {
                        if (Operation == "Sum")
                        {
                            exisiting.Total += item.Total;
                            exisiting.SellVolume += item.SellVolume;
                            exisiting.BuyVolume += item.BuyVolume;
                        }
                        else if (Operation == "Diff")
                        {
                            exisiting.Total -= item.Total;
                            exisiting.SellVolume -= item.SellVolume;
                            exisiting.BuyVolume -= item.BuyVolume;
                        }

                        return exisiting;
                    });
            }

            foreach (var item in vol2)
            {
                dictionary.AddOrUpdate(item.Price, item,
                    (price, exisiting) =>
                    {
                        if (Operation == "Sum")
                        {
                            exisiting.Total += item.Total;
                            exisiting.SellVolume += item.SellVolume;
                            exisiting.BuyVolume += item.BuyVolume;
                        }
                        else if (Operation == "Diff")
                        {
                            exisiting.Total -= item.Total;
                            exisiting.SellVolume -= item.SellVolume;
                            exisiting.BuyVolume -= item.BuyVolume;
                        }

                        return exisiting;
                    });
            }

            return dictionary.Select(e=>e.Value).ToList();
        }
    }
}
