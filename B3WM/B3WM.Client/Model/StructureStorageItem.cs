using Magic.IndexedDb;
using Magic.IndexedDb.SchemaAnnotations;
using static B3WM.Client.Model.StructureStorageItem;

namespace B3WM.Client.Model
{
    public class StructureStorageItem : MagicTableTool<StructureStorageItem>, IMagicTable<DbSets>
    {
        public static readonly IndexedDbSet DataBase = IndexDbContext.DataBase;

        public List<IMagicCompoundIndex> GetCompoundIndexes() =>
        new List<IMagicCompoundIndex>() {
            CreateCompoundIndex(x => x.TimeFrame, x => x.Date, x => x.Symbol)
        };

        public IMagicCompoundKey GetKeys() =>
            CreateCompoundKey(x => x.Symbol,
                x => x.TimeFrame,
                x => x.Date);
        //CreatePrimaryKey(x => x.Id, true); // Auto-incrementing primary key

        public string GetTableName() => "Structures";
        public IndexedDbSet GetDefaultDatabase() => IndexDbContext.DataBase;

        public DbSets Databases { get; } = new();
        public sealed class DbSets
        {
            public readonly IndexedDbSet StructureStorageItem = IndexDbContext.DataBase;
        }

        [MagicName("Date")]
        public DateTime Date { get; set; }

        [MagicName("Symbol")]
        public string Symbol { get; set; }

        //InMinutes
        [MagicName("TimeFrame")]
        public int TimeFrame { get; set; }

        [MagicName("UpBorder")]
        public double UpBorder { get; set; }
        [MagicName("DownBorder")]
        public double DownBorder { get; set; }
        [MagicName("UpAuxBorder")]
        public double UpAuxBorder { get; set; }
        [MagicName("DownAuxBorder")]
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
