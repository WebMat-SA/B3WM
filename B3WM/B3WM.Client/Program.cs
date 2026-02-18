using B3WM.Client.Services;
using Blazored.SessionStorage;
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
            builder.Services.AddScoped<IndexedDbStorageAccessor>();
            builder.Services.AddScoped<ComponentStateService>();
            builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddBlazoredSessionStorage();
            builder.Services.AddScoped<IAccountService, AccountService>();

            // use this to add MudServices and the MudBlazor.Extensions
            builder.Services.AddMudServicesWithExtensions();

            // or this to add only the MudBlazor.Extensions but please ensure that this is added after mud servicdes are added. That means after `AddMudServices`
            builder.Services.AddMudExtensions();

            builder.Services.AddMudServices();

            var culture = new CultureInfo("pt-BR");

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            await builder.Build().RunAsync();
        }
    }
}
