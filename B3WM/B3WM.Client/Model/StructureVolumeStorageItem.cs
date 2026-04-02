using B3WM.Shared.Entity;
using Magic.IndexedDb;
using Magic.IndexedDb.SchemaAnnotations;
using static B3WM.Client.Model.StructureVolumeStorageItem;

namespace B3WM.Client.Model
{
    public class StructureVolumeStorageItem
    {
        public DateTime Date { get; set; }

        public string Symbol { get; set; }

        public List<VolumeLevel>? Peaks { get; set; }

        public List<VolumeLevel>? Valleys { get; set; }
    }
}
