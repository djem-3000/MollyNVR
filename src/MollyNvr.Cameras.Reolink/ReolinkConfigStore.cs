using System.Text.Json;
using System.Text.Json.Nodes;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Cameras.Reolink;

/// <summary>
/// Файл конфигурации моста neolink.net. Мост читает его при старте, поэтому после
/// изменения файла супервизор (entrypoint контейнера или локальный скрипт) перезапускает процесс.
/// </summary>
internal static class ReolinkConfigStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Добавляет или обновляет камеру; возвращает <c>true</c>, если файл изменился.</summary>
    public static async Task<bool> UpsertAsync(
        string path,
        string cameraName,
        string address,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);

        try
        {
            var existing = await ReadTextAsync(path, cancellationToken);
            var root = ParseRoot(existing);
            var cameras = root["cameras"] as JsonArray ?? new JsonArray();

            Remove(cameras, cameraName);
            cameras.Add(new JsonObject
            {
                ["name"] = cameraName,
                ["address"] = address,
                ["username"] = credentials.UserName,
                ["password"] = credentials.Password,
                ["stream"] = "both",
            });
            root["cameras"] = cameras;

            var updated = root.ToJsonString(WriteOptions);
            if (SameContent(existing, updated))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, updated, cancellationToken);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<string?> ReadTextAsync(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;

    private static JsonObject ParseRoot(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonObject { ["webui"] = false, ["cameras"] = new JsonArray() };
        }

        try
        {
            return JsonNode.Parse(text, nodeOptions: null, DocumentOptions) as JsonObject
                ?? throw new JsonException("корневой элемент не объект");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Файл конфигурации моста Reolink повреждён: {exception.Message}",
                exception);
        }
    }

    private static void Remove(JsonArray cameras, string cameraName)
    {
        for (var index = cameras.Count - 1; index >= 0; index--)
        {
            if (cameras[index] is JsonObject camera &&
                string.Equals(
                    camera["name"]?.GetValue<string>(),
                    cameraName,
                    StringComparison.OrdinalIgnoreCase))
            {
                cameras.RemoveAt(index);
            }
        }
    }

    private static bool SameContent(string? existing, string updated) =>
        existing is not null &&
        string.Equals(
            existing.ReplaceLineEndings("\n").Trim(),
            updated.ReplaceLineEndings("\n").Trim(),
            StringComparison.Ordinal);
}
