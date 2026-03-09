using BlazorWorker.Extensions.JSRuntime;
using BlazorWorker.WorkerCore;
using Magic.IndexedDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace B3WM.Client.Services
{
    public class MainHelperStartup
    {
        public readonly IServiceProvider serviceProvider;
        private readonly IWorkerMessageService workerMessageService;

        public MainHelperStartup(IWorkerMessageService workerMessageService)
        {

            this.workerMessageService = workerMessageService;

            serviceProvider = ServiceCollectionHelper.BuildServiceProviderFromMethod(Configure);
        }

        public T Resolve<T>() => serviceProvider.GetService<T>();

        public void Configure(IServiceCollection services)
        {
            services.AddBlazorWorkerJsRuntime()
                .AddMagicBlazorDB(BlazorInteropMode.WASM, true)
                .AddScoped<MainHelper>()
                .AddSingleton(workerMessageService);
        }

    }
}
