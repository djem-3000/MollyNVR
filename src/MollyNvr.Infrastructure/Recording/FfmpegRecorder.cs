using System.Diagnostics;
using System.Text;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>ffmpeg process that records the camera stream in 5-minute segments.</summary>
internal sealed class FfmpegRecorder
{
    private const int MaxErrorLength = 4000;

    private readonly Process _process;
    private readonly StringBuilder _errors = new();

    private FfmpegRecorder(Guid cameraId, string sessionDirectory, Process process)
    {
        CameraId = cameraId;
        SessionDirectory = sessionDirectory;
        _process = process;
    }

    public Guid CameraId { get; }

    public string SessionDirectory { get; }

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;

    public bool IsRunning
    {
        get
        {
            try
            {
                return !_process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    public string? LastError
    {
        get
        {
            lock (_errors)
            {
                var value = _errors.ToString().Trim();
                return value.Length == 0 ? null : value;
            }
        }
    }

    public static FfmpegRecorder Start(
        string ffmpegPath,
        Guid cameraId,
        string inputUrl,
        string poolPath,
        int segmentSeconds)
    {
        var session = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var sessionDirectory = Path.Combine(poolPath, cameraId.ToString("N"), session);
        Directory.CreateDirectory(sessionDirectory);

        var startInfo = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            WorkingDirectory = sessionDirectory,
        };

        foreach (var argument in BuildArguments(inputUrl, sessionDirectory, segmentSeconds))
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var recorder = new FfmpegRecorder(cameraId, sessionDirectory, process);
        process.ErrorDataReceived += recorder.OnErrorData;
        process.Start();
        process.BeginErrorReadLine();

        return recorder;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning)
        {
            _process.Dispose();
            return;
        }

        try
        {
            await _process.StandardInput.WriteLineAsync("q");
            await _process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
                        // the process has already exited
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await _process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                                // the process has already exited
            }
        }
        finally
        {
            _process.Dispose();
        }
    }

    private void OnErrorData(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data))
        {
            return;
        }

        lock (_errors)
        {
            _errors.AppendLine(args.Data);

            if (_errors.Length > MaxErrorLength)
            {
                _errors.Remove(0, _errors.Length - MaxErrorLength);
            }
        }
    }

    private static IReadOnlyList<string> BuildArguments(
        string inputUrl,
        string sessionDirectory,
        int segmentSeconds) =>
    [
        "-hide_banner",
        "-loglevel", "warning",
        "-rtsp_transport", "tcp",
        "-i", inputUrl,
        "-map", "0:v:0",
        "-map", "0:a:0?",
        "-c:v", "copy",
                // fMP4 does not support G.711/PCM and some other camera codecs — audio is encoded to AAC.
        "-c:a", "aac",
        "-b:a", "128k",
        "-f", "segment",
        "-segment_time", segmentSeconds.ToString(),
        "-segment_format", "mp4",
        "-segment_format_options", "movflags=+frag_keyframe+empty_moov+default_base_moof",
        "-reset_timestamps", "1",
        "-strftime", "1",
        Path.Combine(sessionDirectory, "seg-%Y%m%d-%H%M%S.mp4"),
    ];
}
