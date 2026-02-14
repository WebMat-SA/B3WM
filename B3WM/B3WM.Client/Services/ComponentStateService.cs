using B3WM.Shared.Extensions;
using System.Reflection;
using System.Text.Json;

namespace B3WM.Client.Services
{
    public class ComponentStateService
    {
        private readonly LocalStorageAccessor _storage;

        public ComponentStateService(LocalStorageAccessor storage)
        {
            _storage = storage;
        }

        public async Task SaveAsync(object component, string key)
        {
            var state = ExtractState(component);
            await _storage.SetItemAsync(key, state);
        }

        public async Task RestoreAsync(object component, string key)
        {
            var state = await _storage.GetItemAsync<Dictionary<string, JsonElement>>(key);
            if (state is null)
                return;

            var properties = component.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.IsDefined(typeof(PersistStateAttribute), false));

            foreach (var prop in properties)
            {
                if (state.TryGetValue(prop.Name, out var element))
                {
                    var typedValue = element.Deserialize(prop.PropertyType);
                    prop.SetValue(component, typedValue);
                }
            }
        }


        private Dictionary<string, object?> ExtractState(object component)
        {
            var properties = component.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.IsDefined(typeof(PersistStateAttribute), false));

            var dict = new Dictionary<string, object?>();

            foreach (var prop in properties)
            {
                dict[prop.Name] = prop.GetValue(component);
            }

            return dict;
        }

        public string CreateSnapshot(object component)
        {
            var state = ExtractState(component);
            var serilizedState = JsonSerializer.Serialize(state);
            //Console.WriteLine(serilizedState);
            return serilizedState;
        }
    }

}
