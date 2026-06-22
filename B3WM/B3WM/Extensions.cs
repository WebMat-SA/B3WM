using B3WM.Services;
using B3WM.Services.Backtest;
using B3WM.Services.Core;
using B3WM.Shared.Interfaces;
using B3WM.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace B3WM
{
    public static class Extensions
    {
        public static IServiceCollection AddCustomService(this IServiceCollection services, IConfiguration config)
        {
            //serviços uteis
            services.AddScoped<DataKeeperBase>(); //serviço que grava e le arquivos json no server
            services.AddScoped<BacktestEngine>();

            services.AddWinfutServices(config);

            services.AddWdofutServices(config);

            return services;
        }

        public static IServiceCollection AddWinfutServices(this IServiceCollection services, IConfiguration config)
        {
            foreach (var timeframe in Defaults.TimeFrames)
            {
                services.AddSingleton(sp => new CandleService(Defaults.Symbols.WINFUT, timeframe, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
                services.AddSingleton(sp => new StructureService(Defaults.Symbols.WINFUT, timeframe, Defaults.WINFUT.MinDistanceUpdateBorder, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
            }
            services.AddSingleton(sp => new BubbleService(Defaults.Symbols.WINFUT, Defaults.WINFUT.ThresholdBubbleSize, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
            services.AddSingleton(sp => new VolumeService(Defaults.Symbols.WINFUT, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));

            services.AddSingleton<OrchestratorService>(sp =>
                new OrchestratorService(
                    Defaults.Symbols.WINFUT,
                    sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(),
                    sp.GetServices<CandleService>(),
                    sp.GetServices<BubbleService>(),
                    sp.GetServices<VolumeService>(),
                    sp.GetServices<StructureService>())
                );
            services.AddSingleton<TickChannelService>(sp => new TickChannelService(Defaults.Symbols.WINFUT));

            services.AddSingleton(sp => new TickProcessorService(Defaults.Symbols.WINFUT, sp.GetServices<TickChannelService>(), sp.GetServices<OrchestratorService>()));
            services.AddSingleton<IHostedService>(sp => sp.GetServices<TickProcessorService>().First(s => s.Symbol == Defaults.Symbols.WINFUT));

            services.AddSingleton(sp => new ThrottlingService(Defaults.Symbols.WINFUT, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
            services.AddSingleton<IHostedService>(sp => sp.GetServices<ThrottlingService>().First(s => s.Symbol == Defaults.Symbols.WINFUT));

            return services;
        }

        public static IServiceCollection AddWdofutServices(this IServiceCollection services, IConfiguration config)
        {
            foreach (var timeframe in Defaults.TimeFrames)
            {
                services.AddSingleton(sp => new CandleService(Defaults.Symbols.WDOFUT, timeframe, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
                services.AddSingleton(sp => new StructureService(Defaults.Symbols.WDOFUT, timeframe, Defaults.WDOFUT.MinDistanceUpdateBorder, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
            }
            services.AddSingleton(sp => new BubbleService(Defaults.Symbols.WDOFUT, Defaults.WDOFUT.ThresholdBubbleSize, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
            services.AddSingleton(sp => new VolumeService(Defaults.Symbols.WDOFUT, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));

            services.AddSingleton<OrchestratorService>(sp =>
                new OrchestratorService(
                    Defaults.Symbols.WDOFUT,
                    sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(),
                    sp.GetServices<CandleService>(),
                    sp.GetServices<BubbleService>(),
                    sp.GetServices<VolumeService>(),
                    sp.GetServices<StructureService>())
                );
            services.AddSingleton<TickChannelService>(sp => new TickChannelService(Defaults.Symbols.WDOFUT));
            
            services.AddSingleton(sp => new TickProcessorService(Defaults.Symbols.WDOFUT, sp.GetServices<TickChannelService>(), sp.GetServices<OrchestratorService>()));
            services.AddSingleton<IHostedService>(sp => sp.GetServices<TickProcessorService>().First(s => s.Symbol == Defaults.Symbols.WDOFUT));

            services.AddSingleton(sp => new ThrottlingService( Defaults.Symbols.WDOFUT, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp));
            services.AddSingleton<IHostedService>(sp => sp.GetServices<ThrottlingService>().First(s => s.Symbol == Defaults.Symbols.WDOFUT));

            return services;
        }
    }
}
