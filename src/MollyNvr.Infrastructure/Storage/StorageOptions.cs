using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MollyNvr.Infrastructure.Storage;

/// <summary>Storage directories: database, credential encryption key.</summary>
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
        /// Determines the data directory: <c>Storage:DataDirectory</c> from configuration,
        /// otherwise <c>/data</c> (container), otherwise <c>data</c> next to the application.
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
