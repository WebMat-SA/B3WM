namespace B3WM.Services.Core
{
    public class DataKeeperService<T> : IDataKeeperService where T : notnull, new() 
    {
        private readonly IServiceProvider _serviceProvider;

        public T DataKeep { get; set; } = default!;

        public DataKeeperService(IServiceProvider serviceProvider) 
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<T> GetDataAsync()
        {
            //fazer aqui para que o DataKeeperBase seja injetado e resolvido pelo serviço de injeção de dependência
            var dataKeeper = _serviceProvider.CreateScope().ServiceProvider.GetService<DataKeeperBase>();

            if (dataKeeper == null)
            {
                throw new InvalidOperationException($"Data keeper not found.");
            }

            return await dataKeeper.ReadDataAsync<T>(Path);
        }

        public async Task SetDataAsync(T data)
        {
            var dataKeeper = _serviceProvider.CreateScope().ServiceProvider.GetService<DataKeeperBase>();
            if (dataKeeper == null)
            {
                throw new InvalidOperationException($"Data keeper not found.");
            }

            await dataKeeper.WriteDataAsync(Path, data);
        }

        public async Task LoadAsync()
        {
            try 
            { 
                DataKeep = await GetDataAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while loading data for {Path}: {ex.Message}");
            }
        }

        public virtual string Path => throw new Exception("Implement Path on your service");
    }

    public interface IDataKeeperService
    {
        string Path { get; }
    }
}
