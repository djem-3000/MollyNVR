using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MollyNvr.Infrastructure.Storage;

/// <summary>Каталоги хранения: БД, ключ шифрования учётных данных.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    private StorageOptions(string dataDirectory, string databasePath, string secretKeyPath)
    {
        DataDirectory = dataDirectory;
        DatabasePath = databasePath;
        SecretKeyPath = secretKeyPath;
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string SecretKeyPath { get; }

    /// <summary>
    /// Определяет каталог данных: <c>Storage:DataDirectory</c> из конфигурации,
    /// иначе <c>/data</c> (контейнер), иначе <c>data</c> рядом с приложением.
    /// </summary>
    public static StorageOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredDirectory = configuration[$"{SectionName}:DataDirectory"];
        var dataDirectory = !string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.GetFullPath(configuredDirectory)
            : Directory.Exists("/data")
                ? "/data"
                : Path.Combine(environment.ContentRootPath, "data");

        var configuredDatabase = configuration[$"{SectionName}:DatabasePath"];
        var databasePath = !string.IsNullOrWhiteSpace(configuredDatabase)
            ? Path.GetFullPath(configuredDatabase)
            : Path.Combine(dataDirectory, "mollynvr.db");

        var configuredKey = configuration[$"{SectionName}:SecretKeyPath"];
        var secretKeyPath = !string.IsNullOrWhiteSpace(configuredKey)
            ? Path.GetFullPath(configuredKey)
            : Path.Combine(dataDirectory, "secret.key");

        return new StorageOptions(dataDirectory, databasePath, secretKeyPath);
    }
}
