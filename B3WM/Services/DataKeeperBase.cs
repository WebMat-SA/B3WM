using System.Text.Json;

namespace B3WM.Services
{
    public class DataKeeperBase
    {
        public virtual async Task<T> ReadDataAsync<T>(string path) where T : new()
        {
            try
            {
                if (!Directory.Exists("Data"))
                    Directory.CreateDirectory("Data");

                string fullPath = Path.Combine("Data", path);

                if (!File.Exists(fullPath))
                {
                    return new T();
                }

                string json = await File.ReadAllTextAsync(fullPath);

                //arquivo corrompido (ex: gravacao interrompida que deixou bytes nulos):
                //isola o arquivo para nao derrubar a leitura e retorna dado vazio para ser regenerado
                if (json.Contains('\0'))
                {
                    Console.WriteLine($"Warning: {path} is corrupted (contains NUL bytes). Quarantining and returning empty data.");
                    QuarantineFile(fullPath, path);
                    return new T();
                }

                return JsonSerializer.Deserialize<T>(json)
                    ?? new T();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Warning: {path} contains invalid JSON ({ex.Message}). Quarantining and returning empty data.");
                QuarantineFile(Path.Combine("Data", path), path);
                return new T();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while reading data from {path}: {ex.Message}");
                throw;
            }
        }

        public virtual async Task WriteDataAsync<T>(string path, T data)
        {
            try
            {
                string json = JsonSerializer.Serialize(
                    data,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                if (!Directory.Exists("Data"))
                    Directory.CreateDirectory("Data");

                string fullPath = Path.Combine("Data", path);
                string tmpPath = fullPath + ".tmp";

                //gravacao atomica: escreve em arquivo temporario e move por cima,
                //evitando que uma gravacao interrompida deixe o arquivo corrompido
                await File.WriteAllTextAsync(tmpPath, json);

                File.Move(tmpPath, fullPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while writing data to {path}: {ex.Message}");
                throw;
            }
        }

        private static void QuarantineFile(string fullPath, string path)
        {
            try
            {
                if (!File.Exists(fullPath))
                    return;

                var quarantinePath = $"{fullPath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(fullPath, quarantinePath);
                Console.WriteLine($"Quarantined corrupted file {path} to {Path.GetFileName(quarantinePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to quarantine corrupted file {path}: {ex.Message}");
            }
        }
    }
}
