namespace B3WM.Services.Core
{
    public class DataKeeperService<T> : IDataKeeperService where T : notnull, new() 
    {
        private static readonly SemaphoreSlim _fileSemaphore = new(1, 1);
        public readonly IServiceProvider _serviceProvider;

        public virtual string Path => throw new Exception("Implement Path on your service");

        public T DataKeep { get; set; } = default!;

        public DataKeeperService(IServiceProvider serviceProvider) 
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<T> GetDataAsync(string? path = null)
        {
            //fazer aqui para que o DataKeeperBase seja injetado e resolvido pelo serviço de injeção de dependência
            var dataKeeper = _serviceProvider.CreateScope().ServiceProvider.GetService<DataKeeperBase>();

            if (dataKeeper == null)
            {
                throw new InvalidOperationException($"Data keeper not found.");
            }

            return await dataKeeper.ReadDataAsync<T>(path ?? Path);
        }

        public async Task SetDataAsync(T data)
        {
            using var scope = _serviceProvider.CreateScope();

            var dataKeeper = scope.ServiceProvider.GetService<DataKeeperBase>();

            if (dataKeeper == null)
                throw new InvalidOperationException("Data keeper not found.");

            await _fileSemaphore.WaitAsync();

            try
            {
                await dataKeeper.WriteDataAsync(Path, data);
                DataKeep = data;
            }
            finally
            {
                _fileSemaphore.Release();
            }
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

    }

    public interface IDataKeeperService
    {
        string Path { get; }
    }
}
