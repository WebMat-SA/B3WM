using Microsoft.JSInterop;
using System.Text.Json;

namespace B3WM.Client.Services
{
    public class IndexedDbStorageAccessor : IAsyncDisposable
    {
        private Lazy<IJSObjectReference> _accessorJsRef = new();
        private readonly IJSRuntime _jsRuntime;

        private bool _initialized = false;

        private const string DatabaseName = "B3WM.Database";
        private const int DatabaseVersion = 1;

        private static readonly string[] Stores =
        {
            "Bars",
            "Bubbles",
            "VolumeLevels",
            "Ticks"
        };

        //private readonly JsonSerializerOptions _serializeOptions = new()
        //{
        //    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        //    IgnoreReadOnlyProperties = true,
        //    IgnoreReadOnlyFields = true,
        //};

        //private readonly JsonSerializerOptions _deserializeOptions = new()
        //{
        //    PropertyNameCaseInsensitive = true,
        //    IgnoreReadOnlyProperties = true,
        //    IgnoreReadOnlyFields = true,
        //    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        //};

        public IndexedDbStorageAccessor(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        private async Task WaitForReference()
        {
            if (!_accessorJsRef.IsValueCreated)
            {
                _accessorJsRef = new(await _jsRuntime
                    .InvokeAsync<IJSObjectReference>("import", "/js/IndexedDbStorageAccessor.js"));
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_accessorJsRef.IsValueCreated)
            {
                await _accessorJsRef.Value.DisposeAsync();
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (_initialized)
                return;

            await WaitForReference();

            await _accessorJsRef.Value.InvokeVoidAsync(
                "initialize",
                DatabaseName,
                DatabaseVersion,
                Stores);

            _initialized = true;
        }

        // ========================================
        // ADD (falha se já existir)
        // ========================================
        public async Task AddAsync<T>(string store, T value)
        {
            await EnsureInitializedAsync();

            await _accessorJsRef.Value.InvokeVoidAsync("add", store, value);
        }

        // ========================================
        // PUT (AddOrUpdate automático)
        // ========================================
        public async Task PutAsync<T>(string store, T value)
        {
            await EnsureInitializedAsync();

            await _accessorJsRef.Value.InvokeVoidAsync("put", store, value);
        }

        // ========================================
        // GET by key
        // ========================================
        public async Task<T?> GetAsync<T>(string store, object key)
        {
            await EnsureInitializedAsync();

            var result = await _accessorJsRef.Value.InvokeAsync<T>(
                "get",
                store,
                key);

            return result;
        }

        // ========================================
        // GET ALL
        // ========================================
        public async Task<List<T>> GetAllAsync<T>(string store)
        {
            await EnsureInitializedAsync();

            var result = await _accessorJsRef.Value.InvokeAsync<List<T>>(
                "getAll",
                store);

            return result ?? new List<T>();
        }

        // ========================================
        // REMOVE
        // ========================================
        public async Task RemoveAsync(string store, object key)
        {
            await EnsureInitializedAsync();

            await _accessorJsRef.Value.InvokeVoidAsync("remove", store, key);
        }

        // ========================================
        // CLEAR STORE
        // ========================================
        public async Task ClearAsync(string store)
        {
            await EnsureInitializedAsync();

            await _accessorJsRef.Value.InvokeVoidAsync("clear", store);
        }

        // ========================================
        // DELETE DATABASE
        // ========================================
        public async Task DeleteDatabaseAsync(string databaseName)
        {
            await EnsureInitializedAsync();

            await _accessorJsRef.Value.InvokeVoidAsync("deleteDatabase", databaseName);
        }

        // ========================================
        // AddOrUpdate com Merge customizável
        // ========================================
        public async Task AddOrUpdateAsync<T>(
            string store,
            object key,
            T newValue,
            Func<T, T, T> mergeFunc)
        {
            var existing = await GetAsync<T>(store, key);

            if (existing is null)
            {
                await PutAsync(store, newValue);
                return;
            }

            var merged = mergeFunc(existing, newValue);

            await PutAsync(store, merged);
        }
    }
}
