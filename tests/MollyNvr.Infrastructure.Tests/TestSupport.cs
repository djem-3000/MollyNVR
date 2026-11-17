using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Tests;

internal static class TestSupport
{
    public static StorageOptions CreateStorageOptions(string directory) =>
        StorageOptions.Resolve(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{StorageOptions.SectionName}:DataDirectory"] = directory,
                })
                .Build(),
            new FakeHostEnvironment(directory));
}

internal sealed class FakeHostEnvironment(string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "MollyNvr.Tests";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "mollynvr-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Временные файлы могли остаться заблокированными — каталог в TEMP безопасен.
        }
        catch (UnauthorizedAccessException)
        {
            // То же самое: тестовый мусор не влияет на результат.
        }
    }
}
