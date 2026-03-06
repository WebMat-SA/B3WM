using B3WM.Client.Components;
using B3WM.Client.Services;
using Blazored.SessionStorage;
using BlazorWorker.Core;
using Magic.IndexedDb;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Extensions;
using MudBlazor.Services;
using System.Globalization;

namespace B3WM.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddScoped<LocalStorageAccessor>();
            builder.Services.AddScoped<ComponentStateService>();
            builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddBlazoredSessionStorage();

            // use this to add MudServices and the MudBlazor.Extensions
            builder.Services.AddMudServicesWithExtensions();

            // Add BlazorWorker services
            builder.Services.AddWorkerFactory();

            // Add magic indexedb - Default safe message limit for WASM applications
            builder.Services.AddMagicBlazorDB(BlazorInteropMode.WASM, builder.HostEnvironment.IsDevelopment());

            var culture = new CultureInfo("pt-BR");

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            await builder.Build().RunAsync();
        }
    }
}
