using Magic.IndexedDb;
using System.Linq.Expressions;

namespace B3WM.Client.Services
{
    public static class MagicExtensions
    {
        public static async Task AddOrUpdateRangeAsync<T, TKey>(this IMagicQuery<T> query,IEnumerable<T> items,Func<T, TKey> keySelector,Func<T, T, T>? updateFunc = null)
        where T : class
        where TKey : notnull
        {
            var list = items.ToList();

            if (list.Count == 0)
                return;

            // pega todas as chaves
            var keys = list.Select(keySelector).Distinct().ToList();

            // carrega registros existentes
            var existing = await query.ToListAsync();

            var dictExisting = existing
                .ToDictionary(keySelector, x => x);

            List<T> toAdd = new();
            List<T> toUpdate = new();

            foreach (var item in list)
            {
                var key = keySelector(item);

                if (dictExisting.TryGetValue(key, out var exist))
                {
                    var updated = updateFunc != null
                        ? updateFunc(exist, item)
                        : item;

                    toUpdate.Add(updated);
                }
                else
                {
                    toAdd.Add(item);
                }
            }

            if (toAdd.Count > 0)
                await query.AddRangeAsync(toAdd);

            if (toUpdate.Count > 0)
                await query.UpdateRangeAsync(toUpdate);
        }
    }
}
