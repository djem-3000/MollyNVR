using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Streams;
using MollyNvr.Domain.Streams;

namespace MollyNvr.Infrastructure.Media;

/// <summary>Starts go2rtc as a child process, watches readiness and restarts it on failure.</summary>
public sealed partial class Go2RtcProcessService(
    IOptions<Go2RtcOptions> options,
    IHostEnvironment environment,
    IMediaEngine mediaEngine,
    StreamRegistry registry,
    CameraRegistry cameraRegistry,
    ILogger<Go2RtcProcessService> logger) : BackgroundService
{
    private static readonly TimeSpan ReadinessTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ReadinessPollDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var binaryPath = ResolveBinaryPath();
        if (binaryPath is null)
        {
            logger.LogError(
                                "go2rtc not found: set Go2Rtc:BinaryPath or place the binary in tools/ (dev) or /usr/local/bin/go2rtc (container)");
            return;
        }

        DeleteLegacyConfig();

        var configPath = WriteConfigFile();
        logger.LogInformation("Media engine: {Binary} (config: {Config})", binaryPath, configPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            var process = StartProcess(binaryPath, configPath);
            try
            {
                await WaitForReadyAsync(stoppingToken);
                logger.LogInformation("go2rtc is ready, API: {BaseUrl}", options.Value.BaseUrl);

                await registry.SyncAsync(stoppingToken);

                try
                {
                    await cameraRegistry.SyncStreamsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                                        logger.LogError(exception, "Failed to register camera streams in go2rtc");
                }

                await process.WaitForExitAsync(stoppingToken);
                                logger.LogWarning("go2rtc exited with code {ExitCode}", process.ExitCode);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                                logger.LogError(exception, "go2rtc process failure");
            }
            finally
            {
                StopProcess(process);
                process.Dispose();
            }

            try
            {
                await Task.Delay(RestartDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task WaitForReadyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ReadinessTimeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await mediaEngine.GetStreamIdsAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                exception is HttpRequestException or TaskCanceledException or MediaEngineException)
            {
                await Task.Delay(ReadinessPollDelay, cancellationToken);
            }
        }

                throw new TimeoutException("go2rtc did not answer the API request in time");
    }

    private string? ResolveBinaryPath()
    {
        var executable = OperatingSystem.IsWindows() ? "go2rtc.exe" : "go2rtc";
        var candidates = new List<string?>
        {
            options.Value.BinaryPath,
            Path.Combine(environment.ContentRootPath, "tools", executable),
            Path.Combine(environment.ContentRootPath, executable),
            Path.Combine("/usr/local/bin", executable)
        };

        var directory = new DirectoryInfo(environment.ContentRootPath);
        for (var depth = 0; depth < 5 && directory is not null; depth++)
        {
            candidates.Add(Path.Combine(directory.FullName, "tools", executable));
            directory = directory.Parent;
        }

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private string WriteConfigFile()
    {
        var configPath = options.Value.ConfigPath;
        if (string.IsNullOrWhiteSpace(configPath))
        {
            configPath = Path.Combine(ResolveConfigDirectory(), "go2rtc.yaml");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(configPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(configPath, BuildDefaultConfig());
                logger.LogInformation("go2rtc config: {Config}", configPath);
        return configPath;
    }

    /// <summary>
        /// go2rtc appends sources with camera credentials to its config.
        /// Keep it outside the /data volume (tmpfs or a temporary directory) so credentials never reach the disk.
    /// </summary>
    private static string ResolveConfigDirectory()
    {
        if (!OperatingSystem.IsWindows() && Directory.Exists("/dev/shm"))
        {
            return "/dev/shm/mollynvr";
        }

        return Path.Combine(Path.GetTempPath(), "mollynvr");
    }

        /// <summary>Removes the old config from /data, where sources with passwords could remain.</summary>
    private void DeleteLegacyConfig()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.ConfigPath))
        {
            return;
        }

        var legacyPath = Path.Combine(ResolveDataDirectory(), "go2rtc.yaml");

        try
        {
            if (File.Exists(legacyPath))
            {
                File.Delete(legacyPath);
                                logger.LogWarning("Removed an outdated go2rtc config with credentials: {Path}", legacyPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
                        logger.LogWarning(exception, "Failed to remove the outdated go2rtc config: {Path}", legacyPath);
        }
    }

    private string BuildDefaultConfig()
    {
        var baseUri = new Uri(options.Value.BaseUrl, UriKind.Absolute);
        var config = new StringBuilder();
        config.AppendLine("api:");
        config.AppendLine($"  listen: \"{baseUri.Host}:{baseUri.Port}\"");
        config.AppendLine();
        config.AppendLine("webrtc:");
        config.AppendLine($"  listen: \"{options.Value.WebRtcListen}\"");

        if (options.Value.WebRtcCandidates.Length > 0)
        {
            config.AppendLine("  candidates:");
            foreach (var candidate in options.Value.WebRtcCandidates)
            {
                config.AppendLine($"    - \"{candidate}\"");
            }
        }

                // Internal RTSP server: ffmpeg sources and transcodes are published as rtsp://localhost:8554/<id>,
                // from where go2rtc pulls them for WebRTC. Not exposed externally (loopback only).
        config.AppendLine();
        config.AppendLine("rtsp:");
        config.AppendLine($"  listen: \"{options.Value.RtspListen}\"");
        config.AppendLine();
        config.AppendLine("log:");
        config.AppendLine("  level: info");
        return config.ToString();
    }

    private string ResolveDataDirectory()
        => Directory.Exists("/data") ? "/data" : Path.Combine(environment.ContentRootPath, "data");

    private Process StartProcess(string binaryPath, string configPath)
    {
        var startInfo = new ProcessStartInfo(binaryPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = environment.ContentRootPath
        };
        startInfo.ArgumentList.Add("-config");
        startInfo.ArgumentList.Add(configPath);

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, args) => ForwardLine(args.Data);
        process.ErrorDataReceived += (_, args) => ForwardLine(args.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception exception)
        {
                        logger.LogDebug(exception, "Failed to stop go2rtc cleanly");
        }
    }

    private void ForwardLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var safeLine = CredentialsInUrl().Replace(line, "${scheme}${user}:***@");
        var level = line.Contains(" ERR ", StringComparison.Ordinal) || line.Contains(" WRN ", StringComparison.Ordinal)
            ? LogLevel.Warning
            : line.Contains(" INF ", StringComparison.Ordinal) ? LogLevel.Information : LogLevel.Debug;
        logger.Log(level, "[go2rtc] {Line}", safeLine);
    }

    [GeneratedRegex(@"(?<scheme>[a-z][a-z0-9+.-]*://)(?<user>[^:@/\s]+):(?<pass>[^@/\s]+)@", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialsInUrl();
}
