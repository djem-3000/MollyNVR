using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Recording;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Infrastructure.Media;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>
/// Держит процессы ffmpeg запущенными по расписанию камер, индексирует готовые
/// сегменты и следит за квотой пула.
/// </summary>
public sealed class RecordingWorker(
    IRecordingSettingsStore settingsStore,
    IStorageSettingsStore storageStore,
    ICameraRepository cameraRepository,
    IRecordingStatusRegistry statusRegistry,
    IRecordingSettingsNotifier settingsNotifier,
    RecordingOptions options,
    IOptions<Go2RtcOptions> go2rtcOptions,
    SegmentIndexer indexer,
    StorageRetention retention,
    ILogger<RecordingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan IndexInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetentionInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<Guid, FfmpegRecorder> _recorders = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _retryAfter = new();
    private readonly string? _go2rtcRtspBase = BuildGo2RtcBase(go2rtcOptions.Value.RtspListen);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SeedStorageAsync(stoppingToken);

        var lastIndex = DateTimeOffset.MinValue;
        var lastRetention = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var storage = await storageStore.GetAsync(stoppingToken);
                var cameras = await cameraRepository.ListAsync(stoppingToken);
                var settings = await settingsStore.ListAsync(stoppingToken);

                await SyncRecordersAsync(cameras, settings, storage, stoppingToken);

                var now = DateTimeOffset.UtcNow;

                if (now - lastIndex >= IndexInterval)
                {
                    await indexer.RunAsync(storage, settings, stoppingToken);
                    lastIndex = now;
                }

                if (now - lastRetention >= RetentionInterval)
                {
                    await retention.RunAsync(storage, stoppingToken);
                    lastRetention = now;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ошибка цикла записи");
            }

            try
            {
                await settingsNotifier.WaitAsync(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await StopAllAsync(stoppingToken);
    }

    private async Task SeedStorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var storage = await storageStore.GetAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(storage.Path))
            {
                storage = storage with { Path = options.DefaultStoragePath };
                await storageStore.SaveAsync(storage, cancellationToken);
            }

            Directory.CreateDirectory(storage.Path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Каталог записей недоступен: {Path}", options.DefaultStoragePath);
        }
    }

    private async Task SyncRecordersAsync(
        IReadOnlyList<Domain.Cameras.Camera> cameras,
        IReadOnlyList<RecordingSettings> settingsList,
        StorageSettings storage,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;

        if (string.IsNullOrWhiteSpace(storage.Path))
        {
            foreach (var cameraId in _recorders.Keys.ToList())
            {
                await StopRecorderAsync(cameraId, cancellationToken, clearError: false);
            }

            return;
        }

        var settingsByCamera = settingsList.ToDictionary(static settings => settings.CameraId);
        var knownCameras = cameras.Select(static camera => camera.Id).ToHashSet();

        foreach (var camera in cameras)
        {
            if (!settingsByCamera.TryGetValue(camera.Id, out var settings) ||
                !settings.HasActiveMode ||
                !settings.ShouldRecord(now))
            {
                await StopRecorderAsync(camera.Id, cancellationToken, clearError: true);
                continue;
            }

            if (_recorders.TryGetValue(camera.Id, out var running))
            {
                if (running.IsRunning)
                {
                    statusRegistry.SetRecording(camera.Id, true);
                    continue;
                }

                await StopRecorderAsync(camera.Id, cancellationToken, clearError: false);
                statusRegistry.SetRecording(camera.Id, false, running.LastError ?? "ffmpeg завершился");
                logger.LogWarning(
                    "ffmpeg камеры «{Camera}» завершился: {Error}",
                    camera.Name,
                    running.LastError ?? "без сообщения");
                _retryAfter[camera.Id] = DateTimeOffset.UtcNow + RetryDelay;
            }

            if (_retryAfter.TryGetValue(camera.Id, out var retryAt) && DateTimeOffset.UtcNow < retryAt)
            {
                continue;
            }

            await StartRecorderAsync(camera, settings, storage, cancellationToken);
        }

        foreach (var cameraId in _recorders.Keys.Where(id => !knownCameras.Contains(id)).ToList())
        {
            await StopRecorderAsync(cameraId, cancellationToken, clearError: true);
        }
    }

    private async Task StartRecorderAsync(
        Domain.Cameras.Camera camera,
        RecordingSettings settings,
        StorageSettings storage,
        CancellationToken cancellationToken)
    {
        var profile = camera.Profiles.FirstOrDefault(candidate => candidate.Id == settings.ProfileId)
            ?? camera.Profiles.FirstOrDefault(candidate => candidate.Id == camera.MainProfileId)
            ?? camera.Profiles.FirstOrDefault();

        if (profile is null)
        {
            statusRegistry.SetRecording(camera.Id, false, "У камеры нет профилей потоков");
            return;
        }

        var credentials = await cameraRepository.GetCredentialsAsync(camera.Id, cancellationToken);
        if (credentials is null)
        {
            statusRegistry.SetRecording(camera.Id, false, "Не найдены учётные данные камеры");
            return;
        }

        try
        {
            var inputUrl = ResolveInputUrl(camera, profile, credentials);
            var recorder = FfmpegRecorder.Start(
                options.FfmpegPath,
                camera.Id,
                inputUrl,
                storage.Path,
                options.SegmentSeconds);

            _recorders[camera.Id] = recorder;
            _retryAfter.TryRemove(camera.Id, out _);
            statusRegistry.SetRecording(camera.Id, true);

            logger.LogInformation(
                "Запись камеры «{Camera}» запущена (профиль {Profile})",
                camera.Name,
                profile.Id);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _retryAfter[camera.Id] = DateTimeOffset.UtcNow + RetryDelay;
            statusRegistry.SetRecording(camera.Id, false, $"ffmpeg не запустился: {exception.Message}");
            logger.LogError(exception, "Не удалось запустить ffmpeg для камеры «{Camera}»", camera.Name);
        }
    }

    /// <summary>
    /// Пишем с внутреннего RTSP go2rtc: камера держит одно соединение, а в списке
    /// процессов нет пароля камеры. Для профилей вне main/sub — прямой URL.
    /// </summary>
    private string ResolveInputUrl(
        Domain.Cameras.Camera camera,
        Domain.Cameras.CameraProfile profile,
        CameraCredentials credentials)
    {
        if (_go2rtcRtspBase is not null)
        {
            if (string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal))
            {
                return $"{_go2rtcRtspBase}/{CameraStreamNames.ForProfile(camera.Id, isMain: true).Value}";
            }

            if (string.Equals(profile.Id, camera.SubProfileId, StringComparison.Ordinal))
            {
                return $"{_go2rtcRtspBase}/{CameraStreamNames.ForProfile(camera.Id, isMain: false).Value}";
            }
        }

        logger.LogInformation(
            "Профиль {Profile} камеры «{Camera}» не опубликован в go2rtc — пишем напрямую с камеры",
            profile.Id,
            camera.Name);

        return RtspUriFactory.WithCredentials(new Uri(profile.RtspUrl), credentials);
    }

    private static string? BuildGo2RtcBase(string? listen)
    {
        if (string.IsNullOrWhiteSpace(listen))
        {
            return null;
        }

        var value = listen.Trim();

        if (value.StartsWith(':'))
        {
            value = "127.0.0.1" + value;
        }

        return $"rtsp://{value}";
    }

    private async Task StopRecorderAsync(Guid cameraId, CancellationToken cancellationToken, bool clearError)
    {
        if (!_recorders.TryRemove(cameraId, out var recorder))
        {
            // Процесса уже нет: состояние записи всё равно снимаем, иначе индикатор REC
            // в UI останется висеть до перезапуска сервиса.
            statusRegistry.SetRecording(cameraId, false, clearError ? null : statusRegistry.LastError(cameraId));
            return;
        }

        // Снимаем признак записи сразу: ffmpeg может завершаться несколько секунд,
        // а индикатор REC в UI не должен висеть после отключения записи.
        statusRegistry.SetRecording(cameraId, false, clearError ? null : recorder.LastError);

        try
        {
            await recorder.StopAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Ошибка остановки ffmpeg камеры {CameraId}", cameraId);
        }

        logger.LogInformation("Запись камеры {CameraId} остановлена", cameraId);
    }

    private async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (var cameraId in _recorders.Keys.ToList())
        {
            await StopRecorderAsync(cameraId, cancellationToken, clearError: true);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await StopAllAsync(cancellationToken);
    }
}
