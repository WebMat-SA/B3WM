using Microsoft.JSInterop;
using System.Text.Json;

namespace B3WM.Client.Services
{
    public class LocalStorageAccessor : IAsyncDisposable
    {
        private Lazy<IJSObjectReference> _accessorJsRef = new();
        private readonly IJSRuntime _jsRuntime;

        public LocalStorageAccessor(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        private async Task WaitForReference()
        {
            if (_accessorJsRef.IsValueCreated is false)
            {
                _accessorJsRef = new(await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/LocalStorageAccessor.js"));
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_accessorJsRef.IsValueCreated)
            {
                await _accessorJsRef.Value.DisposeAsync();
            }
        }

        public async Task<T> GetItemAsync<T>(string key)
        {
            await WaitForReference();
            var json = await _accessorJsRef.Value.InvokeAsync<string>("get", key);

            // Se for fazio retorna null
            if (json is null)
                return default;

            // Converte o json string para o tipo T
            var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                IgnoreReadOnlyProperties = true,
                IgnoreReadOnlyFields = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            });

            return result;
        }

        public async Task SetItemAsync<T>(string key, T value)
        {
            await WaitForReference();

            // Converte o value para json string
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                IgnoreReadOnlyProperties = true,
                IgnoreReadOnlyFields = true,
            });

            await _accessorJsRef.Value.InvokeVoidAsync("set", key, json);
        }

        public async Task ClearAsync()
        {
            await WaitForReference();
            await _accessorJsRef.Value.InvokeVoidAsync("clear");
        }

        public async Task RemoveAsync(string key)
        {
            await WaitForReference();
            await _accessorJsRef.Value.InvokeVoidAsync("remove", key);
        }
    }
}
