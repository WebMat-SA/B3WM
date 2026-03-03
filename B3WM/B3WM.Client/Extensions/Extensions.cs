using B3WM.Shared.Entity;
using BlazorWorker.WorkerBackgroundService;

namespace B3WM.Client.Extensions
{
    public class KnownTypesJsonSerializer : SerializeLinqExpressionJsonSerializerBase
    {
        private static Type[]? _knownTypes;

        public override Type[] GetKnownTypes()
        {
            return new[] { typeof(Ticks2),  };
        }
    }

    public static class ServiceCollectionHelper
    {
        public delegate void Configure(IServiceCollection services);

        public static IServiceProvider BuildServiceProviderFromMethod(Configure configureMethod)
        {
            var serviceCollection = new ServiceCollection();
            configureMethod(serviceCollection);
            return serviceCollection.BuildServiceProvider();
        }
    }
}
