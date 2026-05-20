using B3WM.Services;
using B3WM.Services.Core;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM
{
    public static class Extensions
    {
        public static IServiceCollection AddCandleService(this IServiceCollection services, IConfiguration config)
        {
            //services.AddSingleton(sp => new CandleService(Defaults.WINFUT, 2, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>()));

            return services;
        }
    }
}
