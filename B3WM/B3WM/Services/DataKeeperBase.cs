using System.Text.Json;

namespace B3WM.Services
{
    public class DataKeeperBase
    {
        public async Task<T> ReadDataAsync<T>(string path) where T : new()
        {
            try
            {
                if (!File.Exists("Data/" + path))
                {
                    return new T();
                }

                string json = await File.ReadAllTextAsync("Data/"+ path);

                return JsonSerializer.Deserialize<T>(json)
                    ?? new T();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while reading data from {path}: {ex.Message}");
                throw;
            }
        }

        public async Task WriteDataAsync<T>(string path, T data)
        {
            try
            {
                string json = JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                await File.WriteAllTextAsync("Data/"+path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while writing data to {path}: {ex.Message}");
                throw;
            }
        }
    }
}
