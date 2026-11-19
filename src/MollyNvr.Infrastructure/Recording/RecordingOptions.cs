using Microsoft.Extensions.Configuration;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>Recording parameters: ffmpeg path, segment length, default pool directory.</summary>
public sealed class RecordingOptions
{
    public const string SectionName = "Recording";

        /// <summary>ffmpeg executable (from PATH in the container, ffmpeg.exe locally).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

        /// <summary>Segment length in seconds.</summary>
    public int SegmentSeconds { get; set; } = 300;

        /// <summary>Pool directory when nothing is selected in the pool settings yet.</summary>
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
