using Microsoft.JSInterop;

namespace B3WM.Client.Services
{
    public static class ServiceCollectionHelper
    {
        public delegate void Configure(IServiceCollection services);

        public static IServiceProvider BuildServiceProviderFromMethod(Configure configureMethod)
        {
            var serviceCollection = new ServiceCollection();

            try
            {
                configureMethod(serviceCollection);
                return serviceCollection.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw;
            }
        }
    }
}
