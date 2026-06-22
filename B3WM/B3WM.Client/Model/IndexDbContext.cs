using B3WM.Shared.Entity;
using Magic.IndexedDb;
using Magic.IndexedDb.Interfaces;

namespace B3WM.Client.Model
{
    public class IndexDbContext : IMagicRepository
    {
        public static readonly IndexedDbSet DataBase = new("DataBase");
    }
}
