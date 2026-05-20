using B3WM.Shared.Entity;

namespace B3WM.Shared.Models
{
    public class StructureVolumeStorageItem
    {
        public DateTime Date { get; set; }

        public string Symbol { get; set; }

        public List<VolumeLevel>? Peaks { get; set; }

        public List<VolumeLevel>? Valleys { get; set; }
    }
}
