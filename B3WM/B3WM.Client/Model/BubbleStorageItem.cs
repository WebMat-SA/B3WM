using Magic.IndexedDb;
using Magic.IndexedDb.SchemaAnnotations;
using static B3WM.Client.Model.BubbleStorageItem;
using static B3WM.Shared.Entity.Ticks2;

namespace B3WM.Client.Model
{
    /// <summary>
    /// Bubbles para IndexedDB. KeyPath "id" (case-sensitive). Sem timeframe.
    /// </summary>
    public class BubbleStorageItem : MagicTableTool<BubbleStorageItem>, IMagicTable<DbSets>
    {
        public static readonly IndexedDbSet DataBase = IndexDbContext.DataBase;

        public List<IMagicCompoundIndex> GetCompoundIndexes() =>
        new List<IMagicCompoundIndex>() {
            CreateCompoundIndex(x=> Date,
                X=> Agent,
                x=> ActionType,
                X=> Price,
                x => Symbol)
        };

        public IMagicCompoundKey GetKeys() =>
            CreateCompoundKey(
                x=> Date,
                X=> Agent,
                x=> ActionType,
                X=> Price,
                x => Symbol
                );
        //CreatePrimaryKey(x => x.Id, true); // Auto-incrementing primary key

        public string GetTableName() => "Bubbles";
        public IndexedDbSet GetDefaultDatabase() => IndexDbContext.DataBase;

        public DbSets Databases { get; } = new();
        public sealed class DbSets
        {
            public readonly IndexedDbSet BubbleStorageItem = IndexDbContext.DataBase;
        }

        [MagicName("Price")]
        public double Price { get; set; }

        [MagicName("Agent")]
        public int Agent { get; set; }

        [MagicName("Amount")]
        public decimal Amount { get; set; }

        [MagicName("Date")]
        public DateTime Date { get; set; }

        [MagicName("actionType")]
        public ActionType ActionType { get; set; }

        [MagicName("Symbol")]
        public string Symbol { get; set; }
    }
}
