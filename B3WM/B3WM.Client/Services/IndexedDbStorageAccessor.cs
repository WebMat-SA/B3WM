using B3WM.Shared.Entity;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace B3WM.Client.Services
{
    public class IndexedDbStorageAccessor : IAsyncDisposable
    {
        private Lazy<IJSObjectReference> _accessorJsRef = new();
        private readonly IJSRuntime _jsRuntime;

        private bool _initialized = false;

        private const string DatabaseName = "B3WM.Database";
        private const int DatabaseVersion = 3;

        private static readonly string[] Stores =
        {
            "Ticks",
            "Bars",
            "Bubbles",
            "VolumeLevels"
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
        // GET CHUNK (leitura em blocos via Web Worker - não bloqueia main thread)
        // afterKey: null na primeira chamada; id do último item na próxima (ou time ISO quando minDate).
        // minDate: limite inferior - só retorna ticks com time >= minDate (últimos N dias, etc).
        // Quando minDate é passado, usa índice byTime e retorna em ordem cronológica (ASC).
        // ========================================
        public async Task<(List<T> Items, string? LastKey)> GetChunkAsync<T>(
            string store,
            int chunkSize,
            string? afterKey = null,
            DateTime? minDate = null)
        {
            await WaitForReference();

            var minDateIso = minDate.HasValue ? minDate.Value.ToString("o") : (string?)null;

            var result = await _accessorJsRef.Value.InvokeAsync<ChunkResult<T>>(
                "getChunkFromWorker",
                store,
                chunkSize,
                afterKey ?? "",
                new { minDate = minDateIso });

            return (result?.Items ?? new List<T>(), result?.LastKey);
        }

        private sealed class ChunkResult<T>
        {
            [JsonPropertyName("items")]
            public List<T> Items { get; set; } = new();

            [JsonPropertyName("lastKey")]
            public string? LastKey { get; set; }
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

        private static int _putTicksBatchSequence;

        /// <summary>
        /// Salva um lote de ticks no IndexedDB via Web Worker (thread separada).
        /// A escrita pesada ocorre fora da main thread, evitando impacto no render do gráfico.
        /// </summary>
        public async Task PutTicksBatchAsync(IEnumerable<Ticks2> ticks)
        {
            if (ticks == null) return;
            var swTotal = Stopwatch.StartNew();
            var swConvert = Stopwatch.StartNew();
            var list = ticks.Select(TickStorageItem.FromTick).ToList();
            swConvert.Stop();
            if (list.Count == 0) return;

            await WaitForReference();
            var swPost = Stopwatch.StartNew();
            await _accessorJsRef.Value.InvokeVoidAsync("postTicksToWorker", "Ticks", list);
            swPost.Stop();
            swTotal.Stop();

            var seq = Interlocked.Increment(ref _putTicksBatchSequence);
            HelperPerformanceConfig.LogSampled(
                nameof(IndexedDbStorageAccessor),
                "PutTicksBatchAsync",
                swTotal.ElapsedMilliseconds,
                seq,
                $"convertMs={swConvert.ElapsedMilliseconds} postToWorkerMs={swPost.ElapsedMilliseconds} count={list.Count} [Worker]");
        }

        /// <summary>Salva barras no IndexedDB via Web Worker.</summary>
        public async Task PutBarsBatchAsync(IEnumerable<BarStorageItem> bars)
        {
            if (bars == null) return;
            var list = bars.ToList();
            if (list.Count == 0) return;

            await WaitForReference();
            await _accessorJsRef.Value.InvokeVoidAsync("postTicksToWorker", "Bars", list);
        }

        /// <summary>Retorna barras do timeframe especificado, ordenadas por data.</summary>
        public async Task<List<BarStorageItem>> GetBarsByTimeframeAsync(int timeframe)
        {
            await EnsureInitializedAsync();
            var all = await GetAllAsync<BarStorageItem>("Bars");
            return all.Where(b => b.Timeframe == timeframe).OrderBy(b => b.Date).ToList();
        }

        /// <summary>Salva bubbles no IndexedDB via Web Worker.</summary>
        public async Task PutBubblesBatchAsync(IEnumerable<BubbleStorageItem> items)
        {
            if (items == null) return;
            var list = items.ToList();
            if (list.Count == 0) return;

            await WaitForReference();
            await _accessorJsRef.Value.InvokeVoidAsync("postTicksToWorker", "Bubbles", list);
        }

        /// <summary>Retorna todos os bubbles, ordenados por time.</summary>
        public async Task<List<BubbleStorageItem>> GetAllBubblesAsync()
        {
            await EnsureInitializedAsync();
            var all = await GetAllAsync<BubbleStorageItem>("Bubbles");
            return all.OrderBy(b => b.Time).ToList();
        }

        /// <summary>Salva volume levels no IndexedDB via Web Worker.</summary>
        public async Task PutVolumeLevelsBatchAsync(IEnumerable<VolumeLevelStorageItem> items)
        {
            if (items == null) return;
            var list = items.ToList();
            if (list.Count == 0) return;

            await WaitForReference();
            await _accessorJsRef.Value.InvokeVoidAsync("postTicksToWorker", "VolumeLevels", list);
        }

        /// <summary>Retorna todos os volume levels salvos.</summary>
        public async Task<List<VolumeLevelStorageItem>> GetAllVolumeLevelsAsync()
        {
            await EnsureInitializedAsync();
            return await GetAllAsync<VolumeLevelStorageItem>("VolumeLevels");
        }
    }
}
