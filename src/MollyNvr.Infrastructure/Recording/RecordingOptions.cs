using Microsoft.Extensions.Configuration;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>Параметры записи: путь к ffmpeg, длина сегмента, каталог пула по умолчанию.</summary>
public sealed class RecordingOptions
{
    public const string SectionName = "Recording";

    /// <summary>Исполняемый файл ffmpeg (в контейнере — из PATH, локально — ffmpeg.exe).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>Длина сегмента в секундах.</summary>
    public int SegmentSeconds { get; set; } = 300;

    /// <summary>Каталог пула, если в настройках пула ещё ничего не выбрано.</summary>
    public string DefaultStoragePath { get; set; } = string.Empty;

    public static RecordingOptions Resolve(IConfiguration configuration, StorageOptions storage)
    {
        var options = new RecordingOptions();

        if (configuration[$"{SectionName}:FfmpegPath"] is { Length: > 0 } ffmpegPath)
        {
            options.FfmpegPath = ffmpegPath;
        }

        if (int.TryParse(configuration[$"{SectionName}:SegmentSeconds"], out var segmentSeconds) && segmentSeconds > 0)
        {
            options.SegmentSeconds = segmentSeconds;
        }

        var configuredPath = configuration[$"{SectionName}:StoragePath"];
        options.DefaultStoragePath = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Directory.Exists("/recordings")
                ? "/recordings"
                : Path.Combine(storage.DataDirectory, "recordings");

        return options;
    }
}
