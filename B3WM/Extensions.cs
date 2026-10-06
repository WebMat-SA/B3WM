using B3WM.Services;
using B3WM.Services.AI;
using B3WM.Services.Core;
using B3WM.Services.Market;
using B3WM.Services.Screen;
using B3WM.Services.Strategies;
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
            services.AddScoped<JevService>(); // consumidor Jev/TypeSafe (issue #16, debug-only)
            services.AddScoped<ScreenStateBuilder>(); // estado WYSIWYG p/ strategies
            services.AddScoped<IMarketData, MarketData>(); // dados p/ strategies
            // Strategies que rodam no backend (uma linha por strategy nova):
            services.AddTransient<JevAnalysisStrategy>();
            services.AddTransient<IStrategy, JevAnalysisStrategy>(sp => sp.GetRequiredService<JevAnalysisStrategy>());
            services.AddScoped<StrategyRegistry>();
            services.AddSingleton<StrategyRunner>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<StrategyRunner>());

            services.AddSymbolServices(
                Defaults.Symbols.WINFUT,
                Defaults.WINFUT.ThresholdBubbleSize,
                Defaults.WINFUT.MinDistanceUpdateBorder,
                Defaults.WINFUT.MinDistanceUpdateBorderDaily);

            services.AddSymbolServices(
                Defaults.Symbols.WDOFUT,
                Defaults.WDOFUT.ThresholdBubbleSize,
                Defaults.WDOFUT.MinDistanceUpdateBorder,
                Defaults.WDOFUT.MinDistanceUpdateBorderDaily);

            //pre-carrega a estrutura de todos os symbol/timeframe no startup, fazendo o backfill
            //do dia antes de o servidor aceitar conexoes
            services.AddSingleton<StructurePreloadService>();
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<StructurePreloadService>());

            return services;
        }

        /// <summary>
        /// Registra a família de serviços de um símbolo (cada símbolo tem 1
        /// jogo completo: candles/structures por timeframe + bubble, volume,
        /// extremes, forecast, orchestrator, channel, processor e throttling).
        /// Antes eram dois métodos copiados (Winfut/Wdofut); símbolo novo = 1 chamada.
        /// </summary>
        public static IServiceCollection AddSymbolServices(
            this IServiceCollection services,
            string symbol,
            int thresholdBubbleSize,
            double minDistanceUpdateBorder,
            double minDistanceUpdateBorderDaily)
        {
            foreach (var timeframe in Defaults.TimeFrames)
            {
                // O 1440 (1D) tem ordem de grandeza própria: usa o default diário,
                // governado pela config separada da seção diária (issue #10).
                var minDistance = timeframe == 1440
                    ? minDistanceUpdateBorderDaily
                    : minDistanceUpdateBorder;
                services.AddSingleton(sp => new CandleService(symbol, timeframe, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp, sp.GetRequiredService<ILogger<CandleService>>()));
                services.AddSingleton(sp => new StructureService(symbol, timeframe, minDistance, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp, sp.GetRequiredService<ILogger<StructureService>>()));
            }
            services.AddSingleton(sp => new BubbleService(symbol, thresholdBubbleSize, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp, sp.GetRequiredService<ILogger<BubbleService>>()));
            services.AddSingleton(sp => new VolumeService(symbol, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp, sp.GetRequiredService<ILogger<VolumeService>>()));
            services.AddSingleton(sp => new ExtremeService(symbol, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp, sp.GetRequiredService<ILogger<ExtremeService>>()));
            services.AddSingleton(sp => new AdjustmentForecastService(symbol, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp, sp.GetRequiredService<ILogger<AdjustmentForecastService>>()));

            services.AddSingleton<OrchestratorService>(sp =>
                new OrchestratorService(
                    symbol,
                    sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(),
                    sp.GetServices<CandleService>(),
                    sp.GetServices<BubbleService>(),
                    sp.GetServices<VolumeService>(),
                    sp.GetServices<StructureService>(),
                    sp.GetServices<AdjustmentForecastService>(),
                    sp.GetRequiredService<ILogger<OrchestratorService>>())
                );
            services.AddSingleton<TickChannelService>(sp => new TickChannelService(symbol));

            services.AddSingleton(sp => new TickProcessorService(symbol, sp.GetServices<TickChannelService>(), sp.GetServices<OrchestratorService>(), sp.GetRequiredService<ILogger<TickProcessorService>>()));
            services.AddSingleton<IHostedService>(sp => sp.GetServices<TickProcessorService>().First(s => s.Symbol == symbol));

            services.AddSingleton(sp => new ThrottlingService(symbol, sp.GetRequiredService<IHubContext<DataHub, IDataHubClient>>(), sp, sp.GetRequiredService<ILogger<ThrottlingService>>()));
            services.AddSingleton<IHostedService>(sp => sp.GetServices<ThrottlingService>().First(s => s.Symbol == symbol));

            return services;
        }
    }
}
