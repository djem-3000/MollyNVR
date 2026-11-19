using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MollyNvr.Application.Archive;
using MollyNvr.Infrastructure.Recording;
using MollyNvr.Infrastructure.Storage;

namespace MollyNvr.Infrastructure.Archive;

/// <summary>
/// Prepares HLS from the segment index: ffmpeg concatenates the files and produces a playlist,
/// which is then served by the API together with the fragments.
/// </summary>
public sealed class PlaybackSessionManager : IArchivePlaybackManager
{
    private const int MaxSessions = 12;
    private const int MaxErrorLength = 4000;

    private static readonly TimeSpan PlaylistTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FinishedIdle = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxIdle = TimeSpan.FromMinutes(60);

    private readonly ConcurrentDictionary<Guid, PlaybackSession> _sessions = new();
    private readonly string _cacheRoot;
    private readonly string _ffmpegPath;
    private readonly ILogger<PlaybackSessionManager> _logger;

    public PlaybackSessionManager(
        RecordingOptions recordingOptions,
        StorageOptions storageOptions,
        ILogger<PlaybackSessionManager> logger)
    {
        _ffmpegPath = recordingOptions.FfmpegPath;
        _cacheRoot = Path.Combine(storageOptions.DataDirectory, "playback-cache");
        _logger = logger;
        CleanupStaleDirectories();
    }

    public async Task<ArchivePlaybackSession> StartAsync(
        IReadOnlyList<PlaybackSourceFile> files,
        ArchiveVideoMode mode,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
                        throw new ArgumentException("There are no files to play.", nameof(files));
        }

        var id = Guid.NewGuid();
        var directory = Path.Combine(_cacheRoot, id.ToString("N"));
        Directory.CreateDirectory(directory);
        WriteConcatList(Path.Combine(directory, "concat.txt"), files);

        var process = new Process
        {
            StartInfo = BuildStartInfo(directory, mode),
            EnableRaisingEvents = true,
        };

        var session = new PlaybackSession(id, directory, process);
        process.ErrorDataReceived += session.OnErrorData;
        process.Start();
        process.BeginErrorReadLine();
        _sessions[id] = session;

        try
        {
            var deadline = DateTime.UtcNow + PlaylistTimeout;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsPlayable(directory))
                {
                    EvictExtra(id);
                    return new ArchivePlaybackSession(id, directory);
                }

                if (session.HasExited)
                {
                    var error = session.ErrorText;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                                                ? "ffmpeg exited before creating the HLS playlist."
                                                : $"ffmpeg could not prepare playback: {error}");
                }

                await Task.Delay(100, cancellationToken);
            }

                        throw new InvalidOperationException("ffmpeg did not prepare the HLS playlist in time.");
        }
        catch
        {
            _sessions.TryRemove(id, out _);
            StopInternal(session);
            throw;
        }
    }

    public ArchivePlaybackFile? FindFile(Guid sessionId, string fileName)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
        {
            return null;
        }

        var contentType = ContentTypeFor(safeName);
        if (contentType is null)
        {
            return null;
        }

        var path = Path.Combine(session.Directory, safeName);

                // With temp_file ffmpeg recreates the playlist and fragments: between unlink and rename
                // the file may be missing or empty. A few short retries
                // remove intermittent 500 errors on the first hls.js request.
        var content = ReadWithRetry(path);

        if (content is null)
        {
            return null;
        }

        session.Touch();
        return new ArchivePlaybackFile(contentType, content);
    }

    private static byte[]? ReadWithRetry(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);

                if (buffer.Length > 0)
                {
                    return buffer.ToArray();
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                                // the file is being renamed — retry
            }

            Thread.Sleep(30);
        }

        return null;
    }

    public void Stop(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            StopInternal(session);
        }
    }

        /// <summary>Removes finished and long-unused sessions.</summary>
    public void Cleanup()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var (id, session) in _sessions)
        {
            var idle = now - session.LastAccess;

            if (idle > MaxIdle || (session.HasExited && idle > FinishedIdle))
            {
                Stop(id);
            }
        }
    }

    private void EvictExtra(Guid currentId)
    {
        if (_sessions.Count <= MaxSessions)
        {
            return;
        }

        foreach (var (id, _) in _sessions
                     .Where(pair => pair.Key != currentId)
                     .OrderBy(pair => pair.Value.LastAccess)
                     .Take(_sessions.Count - MaxSessions))
        {
            Stop(id);
        }
    }

    private void StopInternal(PlaybackSession session)
    {
        try
        {
            if (!session.HasExited)
            {
                session.Process.Kill(entireProcessTree: true);

                if (!session.Process.WaitForExit(3000))
                {
                                        _logger.LogWarning("ffmpeg of playback session {SessionId} did not exit within 3 s.", session.Id);
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
                        _logger.LogDebug(exception, "Failed to stop ffmpeg of playback session {SessionId}.", session.Id);
        }
        finally
        {
            session.Process.Dispose();
        }

        DeleteDirectory(session.Directory);
    }

    private void DeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == 2)
                {
                                        _logger.LogWarning(exception, "Failed to delete the playback session directory {Directory}.", directory);
                }
            }
        }
    }

    private void CleanupStaleDirectories()
    {
        try
        {
            Directory.CreateDirectory(_cacheRoot);

            foreach (var directory in Directory.EnumerateDirectories(_cacheRoot))
            {
                DeleteDirectory(directory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
                        _logger.LogWarning(exception, "Failed to clean the playback sessions directory {Directory}.", _cacheRoot);
        }
    }

    private ProcessStartInfo BuildStartInfo(string directory, ArchiveVideoMode mode)
    {
        var startInfo = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
        };

        foreach (var argument in BuildArguments(mode))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static IReadOnlyList<string> BuildArguments(ArchiveVideoMode mode)
    {
        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel", "warning",
            "-nostdin",
            "-y",
            "-f", "concat",
            "-safe", "0",
            "-i", "concat.txt",
            "-map", "0:v:0",
            "-map", "0:a:0?",
        };

        arguments.AddRange(BuildOutputArguments(mode));
        return arguments;
    }

    private static IReadOnlyList<string> BuildOutputArguments(ArchiveVideoMode mode)
    {
        var arguments = new List<string>();

        switch (mode)
        {
            case ArchiveVideoMode.Copy:
                arguments.AddRange(["-c:v", "copy"]);
                break;
            case ArchiveVideoMode.CopyHevc:
                arguments.AddRange(["-c:v", "copy", "-tag:v", "hvc1"]);
                break;
            default:
                arguments.AddRange([
                    "-c:v", "libx264",
                    "-preset", "veryfast",
                    "-crf", "23",
                    "-pix_fmt", "yuv420p",
                ]);
                break;
        }

        arguments.AddRange([
            "-c:a", "aac",
            "-b:a", "96k",
            "-f", "hls",
            "-hls_time", "4",
            "-hls_list_size", "0",
            "-hls_playlist_type", "event",
            "-hls_flags", "independent_segments+temp_file",
            "-hls_segment_type", "fmp4",
            "-hls_fmp4_init_filename", "init.mp4",
            "-hls_segment_filename", "seg%05d.m4s",
            "index.m3u8",
        ]);

        return arguments;
    }

    private static void WriteConcatList(string path, IReadOnlyList<PlaybackSourceFile> files)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ffconcat version 1.0");

        foreach (var file in files)
        {
            builder.Append("file '").Append(EscapePath(file.Path)).AppendLine("'");
        }

        File.WriteAllText(path, builder.ToString());
    }

    private static string EscapePath(string path) =>
        path.Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal);

    private static bool IsPlayable(string directory)
    {
        try
        {
            var playlist = Path.Combine(directory, "index.m3u8");

            if (!File.Exists(playlist) ||
                !File.ReadAllText(playlist).Contains("#EXTINF", StringComparison.Ordinal))
            {
                return false;
            }

            return File.Exists(Path.Combine(directory, "init.mp4")) &&
                   Directory.EnumerateFiles(directory, "*.m4s").Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? ContentTypeFor(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".m3u8" => "application/vnd.apple.mpegurl",
            ".m4s" or ".mp4" => "video/mp4",
            ".ts" => "video/mp2t",
            _ => null,
        };

    private sealed class PlaybackSession(Guid id, string directory, Process process)
    {
        private readonly StringBuilder _errors = new();
        private readonly object _errorLock = new();
        private long _lastAccessTicks = DateTimeOffset.UtcNow.UtcTicks;

        public Guid Id { get; } = id;

        public string Directory { get; } = directory;

        public Process Process { get; } = process;

        public DateTimeOffset LastAccess =>
            new(Interlocked.Read(ref _lastAccessTicks), TimeSpan.Zero);

        public bool HasExited
        {
            get
            {
                try
                {
                    return Process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public string ErrorText
        {
            get
            {
                lock (_errorLock)
                {
                    return _errors.ToString().Trim();
                }
            }
        }

        public void Touch() =>
            Interlocked.Exchange(ref _lastAccessTicks, DateTimeOffset.UtcNow.UtcTicks);

        public void OnErrorData(object sender, DataReceivedEventArgs args)
        {
            if (args.Data is null)
            {
                return;
            }

            lock (_errorLock)
            {
                _errors.AppendLine(args.Data);

                if (_errors.Length > MaxErrorLength)
                {
                    _errors.Remove(0, _errors.Length - MaxErrorLength);
                }
            }
        }
    }
}
