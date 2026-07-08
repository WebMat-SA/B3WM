using B3WM.Shared.Entity;
using Magic.IndexedDb;
using Magic.IndexedDb.SchemaAnnotations;
using static B3WM.Client.Model.BarStorageItem;

namespace B3WM.Client.Model
{
    /// <summary>
    /// Barras para IndexedDB com timeframe. KeyPath "id" (case-sensitive).
    /// </summary>
    public class BarStorageItem : MagicTableTool<BarStorageItem>, IMagicTable<DbSets>
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

        public string GetTableName() => "Bars";
        public IndexedDbSet GetDefaultDatabase() => IndexDbContext.DataBase;

        public DbSets Databases { get; } = new();
        public sealed class DbSets
        {
            public readonly IndexedDbSet BarStorageItem = IndexDbContext.DataBase;
        }

        [MagicName("Date")]
        public DateTime Date { get; set; }

        [MagicName("Symbol")]
        public string Symbol { get; set; }

        //InMinutes
        [MagicName("TimeFrame")]
        public int TimeFrame { get; set; }

        [MagicName("Open")]
        public double Open { get; set; }

        [MagicName("High")]
        public double High { get; set; }

        [MagicName("Low")]
        public double Low { get; set; }

        [MagicName("Close")]
        public double Close { get; set; }

        [MagicName("Volume")]
        public long Volume { get; set; }

        [MagicName("VolumeLevel")]
        public List<VolumeLevel>? VolumeLevel { get; set; }

        [MagicName("ForecastPrice")]
        public double? ForecastPrice { get; set; }

    }
}
