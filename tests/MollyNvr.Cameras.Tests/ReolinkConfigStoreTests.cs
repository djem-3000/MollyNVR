using System.Text.Json.Nodes;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Cameras.Reolink;

namespace MollyNvr.Cameras.Tests;

public class ReolinkConfigStoreTests
{
    [Fact]
    public async Task UpsertAsync_CreatesFileWithCamera()
    {
        using var workspace = new Workspace();

        var changed = await ReolinkConfigStore.UpsertAsync(
            workspace.ConfigPath,
            "cam-1",
            "192.168.1.10",
            new CameraCredentials("user", "pass"),
            CancellationToken.None);

        Assert.True(changed);

        var root = ReadRoot(workspace.ConfigPath);
        Assert.False(root["webui"]!.GetValue<bool>());

        var camera = Assert.Single(root["cameras"]!.AsArray())!.AsObject();
        Assert.Equal("cam-1", camera["name"]!.GetValue<string>());
        Assert.Equal("192.168.1.10", camera["address"]!.GetValue<string>());
        Assert.Equal("user", camera["username"]!.GetValue<string>());
        Assert.Equal("pass", camera["password"]!.GetValue<string>());
        Assert.Equal("both", camera["stream"]!.GetValue<string>());
    }

    [Fact]
    public async Task UpsertAsync_PreservesOtherCamerasAndSettings()
    {
        using var workspace = new Workspace();
        await workspace.WriteAsync("""
            {
              "bind_port": 9000,
              "cameras": [
                { "name": "existing", "address": "192.168.1.20", "username": "a", "password": "b" }
              ]
            }
            """);

        await ReolinkConfigStore.UpsertAsync(
            workspace.ConfigPath,
            "cam-1",
            "192.168.1.10",
            new CameraCredentials("user", "pass"),
            CancellationToken.None);

        var root = ReadRoot(workspace.ConfigPath);
        Assert.Equal(9000, root["bind_port"]!.GetValue<int>());

        var names = root["cameras"]!.AsArray()
            .Select(static camera => camera!["name"]!.GetValue<string>())
            .ToList();

        Assert.Equal(["existing", "cam-1"], names);
    }

    [Fact]
    public async Task UpsertAsync_SameData_DoesNotRewriteFile()
    {
        using var workspace = new Workspace();
        var credentials = new CameraCredentials("user", "pass");

        var first = await ReolinkConfigStore.UpsertAsync(
            workspace.ConfigPath, "cam-1", "192.168.1.10", credentials, CancellationToken.None);
        var second = await ReolinkConfigStore.UpsertAsync(
            workspace.ConfigPath, "cam-1", "192.168.1.10", credentials, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesPasswordWithoutDuplicateCamera()
    {
        using var workspace = new Workspace();
        await ReolinkConfigStore.UpsertAsync(
            workspace.ConfigPath, "cam-1", "192.168.1.10", new CameraCredentials("user", "old"), CancellationToken.None);

        var changed = await ReolinkConfigStore.UpsertAsync(
            workspace.ConfigPath, "cam-1", "192.168.1.10", new CameraCredentials("user", "new"), CancellationToken.None);

        Assert.True(changed);

        var cameras = ReadRoot(workspace.ConfigPath)["cameras"]!.AsArray();
        var camera = Assert.Single(cameras)!.AsObject();
        Assert.Equal("new", camera["password"]!.GetValue<string>());
    }

    private static JsonObject ReadRoot(string path) =>
        JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private sealed class Workspace : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), $"mollynvr-reolink-{Guid.NewGuid():N}");

        public Workspace() => Directory.CreateDirectory(_directory);

        public string ConfigPath => Path.Combine(_directory, "config.json");

        public Task WriteAsync(string content) => File.WriteAllTextAsync(ConfigPath, content);

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
