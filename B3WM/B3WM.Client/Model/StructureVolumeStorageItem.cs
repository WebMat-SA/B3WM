using B3WM.Shared.Entity;
using Magic.IndexedDb;
using Magic.IndexedDb.SchemaAnnotations;
using static B3WM.Client.Model.StructureVolumeStorageItem;

namespace B3WM.Client.Model
{
    public class StructureVolumeStorageItem : MagicTableTool<StructureVolumeStorageItem>, IMagicTable<DbSets>
    {
        public static readonly IndexedDbSet DataBase = IndexDbContext.DataBase;

        public List<IMagicCompoundIndex> GetCompoundIndexes() =>
        new List<IMagicCompoundIndex>() {
            CreateCompoundIndex(x => x.Date, x => x.Symbol)
        };

        public IMagicCompoundKey GetKeys() =>
            CreateCompoundKey(x => x.Symbol,
                x => x.Date);
        //CreatePrimaryKey(x => x.Id, true); // Auto-incrementing primary key

        public string GetTableName() => "StructureVolume";
        public IndexedDbSet GetDefaultDatabase() => IndexDbContext.DataBase;

        public DbSets Databases { get; } = new();
        public sealed class DbSets
        {
            public readonly IndexedDbSet StructureVolumeStorageItem = IndexDbContext.DataBase;
        }


        [MagicName("Date")]
        public DateTime Date { get; set; }

        [MagicName("Symbol")]
        public string Symbol { get; set; }

        [MagicName("VolumeLevel")]
        public List<VolumeLevel>? Peaks { get; set; }

        [MagicName("VolumeLevel")]
        public List<VolumeLevel>? Valleys { get; set; }
    }
}
