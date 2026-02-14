using B3WM.Client.Services;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace B3WM.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddScoped<LocalStorageAccessor>();
            builder.Services.AddScoped<IndexedDbStorageAccessor>();
            builder.Services.AddScoped<ComponentStateService>();
            builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddBlazoredSessionStorage();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddMudServices();

            await builder.Build().RunAsync();
        }
    }
}
