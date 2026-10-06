using System.Text.Json;

namespace B3WM.Services
{
    public class DataKeeperBase
    {
        private readonly ILogger<DataKeeperBase>? _logger;

        /// <summary>Logger opcional (DI injeta automaticamente; `new` manual segue no Console).</summary>
        public DataKeeperBase(ILogger<DataKeeperBase>? logger = null) => _logger = logger;

        private void Warn(string message)
        {
            if (_logger != null) _logger.LogWarning("{Message}", message);
            else Console.WriteLine(message);
        }

        private void Warn(Exception ex, string message)
        {
            if (_logger != null) _logger.LogWarning(ex, "{Message}", message);
            else Console.WriteLine($"{message} ({ex.Message})");
        }

        /// <summary>Diretório raiz dos arquivos de dados (relativo ao cwd).</summary>
        protected virtual string RootDirectory => "Data";

        public virtual async Task<T> ReadDataAsync<T>(string path) where T : new()
        {
            try
            {
                if (!Directory.Exists(RootDirectory))
                    Directory.CreateDirectory(RootDirectory);

                string fullPath = Path.Combine(RootDirectory, path);

                if (!File.Exists(fullPath))
                {
                    return new T();
                }

                string json = await File.ReadAllTextAsync(fullPath);

                //arquivo corrompido (ex: gravacao interrompida que deixou bytes nulos):
                //isola o arquivo para nao derrubar a leitura e retorna dado vazio para ser regenerado
                if (json.Contains('\0'))
                {
                    Warn($"Warning: {path} is corrupted (contains NUL bytes). Quarantining and returning empty data.");
                    QuarantineFile(fullPath, path);
                    return new T();
                }

                return JsonSerializer.Deserialize<T>(json)
                    ?? new T();
            }
            catch (JsonException ex)
            {
                Warn(ex, $"Warning: {path} contains invalid JSON. Quarantining and returning empty data.");
                QuarantineFile(Path.Combine("Data", path), path);
                return new T();
            }
            catch (Exception ex)
            {
                Warn(ex, $"Error occurred while reading data from {path}");
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

                if (!Directory.Exists(RootDirectory))
                    Directory.CreateDirectory(RootDirectory);

                string fullPath = Path.Combine(RootDirectory, path);
                string tmpPath = fullPath + ".tmp";

                //gravacao atomica: escreve em arquivo temporario e move por cima,
                //evitando que uma gravacao interrompida deixe o arquivo corrompido
                await File.WriteAllTextAsync(tmpPath, json);

                File.Move(tmpPath, fullPath, overwrite: true);
            }
            catch (Exception ex)
            {
                Warn(ex, $"Error occurred while writing data to {path}");
                throw;
            }
        }

        private void QuarantineFile(string fullPath, string path)
        {
            try
            {
                if (!File.Exists(fullPath))
                    return;

                var quarantinePath = $"{fullPath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(fullPath, quarantinePath);
                Warn($"Quarantined corrupted file {path} to {Path.GetFileName(quarantinePath)}");
            }
            catch (Exception ex)
            {
                Warn(ex, $"Failed to quarantine corrupted file {path}");
            }
        }
    }
}
