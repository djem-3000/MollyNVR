using System.Net;
using Microsoft.Extensions.Logging;
using MollyNvr.Application.Streams;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Domain.Cameras;
using MollyNvr.Domain.Streams;

namespace MollyNvr.Application.Cameras;

/// <summary>
/// Реестр сохранённых камер: добавление с проверкой подключения, изменение, удаление,
/// синхронизация потоков с медиа-ядром.
/// </summary>
public sealed class CameraRegistry(
    ICameraRepository repository,
    CameraProbeService probeService,
    IMediaEngine mediaEngine,
    ILogger<CameraRegistry> logger)
{
    /// <summary>Список сохранённых камер.</summary>
    public async Task<IReadOnlyList<CameraView>> ListAsync(CancellationToken cancellationToken)
    {
        var cameras = await repository.ListAsync(cancellationToken);
        return cameras.Select(CameraView.From).ToList();
    }

    /// <summary>Камера по идентификатору или <c>null</c>.</summary>
    public async Task<CameraView?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await repository.FindAsync(id, cancellationToken);
        return camera is null ? null : CameraView.From(camera);
    }

    /// <summary>
    /// Запрашивает у сохранённой камеры состояние DHCP.
    /// </summary>
    public async Task<bool?> GetDhcpEnabledAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await repository.FindAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var credentials = await repository.GetCredentialsAsync(id, cancellationToken);
        if (credentials is null)
        {
            return null;
        }

        var endpoint = CreateEndpoint(camera);
        var target = new CameraTarget(endpoint, credentials, camera.VendorKey);
        return await probeService.GetDhcpEnabledAsync(target, cancellationToken);
    }

    /// <summary>Проверяет подключение, сохраняет камеру и регистрирует её потоки в медиа-ядре.</summary>
    public async Task<CameraView> AddAsync(CameraRegistration registration, CancellationToken cancellationToken)
    {
        var credentials = RequireCredentials(registration);
        var endpoint = CreateEndpoint(registration);
        var target = new CameraTarget(endpoint, credentials, NormalizeVendor(registration.VendorKey));

        var (vendorKey, probe) = await probeService.ProbeRawAsync(target, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var profiles = MapProfiles(probe);
        var mainProfileId = ResolveMainProfileId(probe, registration.MainProfileId);

        var camera = Camera.Register(
            name: ResolveName(registration.Name, vendorKey, probe),
            host: endpoint.Host.ToString(),
            rtspPort: endpoint.RtspPort,
            vendorKey: vendorKey,
            vendor: probe.Device.Vendor,
            model: probe.Device.Model,
            firmware: probe.Device.Firmware,
            serialNumber: probe.Device.SerialNumber,
            profiles: profiles,
            mainProfileId: mainProfileId,
            now,
            subProfileId: ResolveSubProfileId(
                profiles,
                mainProfileId,
                NormalizeSubProfileId(registration.SubProfileId) ?? probe.SubProfile?.Id));

        await repository.AddAsync(camera, credentials, cancellationToken);
        await TryUpsertStreamsAsync(camera, credentials, cancellationToken);

        return CameraView.From(camera);
    }

    /// <summary>Повторно опрашивает камеру и обновляет адрес, профили и учётные данные.</summary>
    public async Task<CameraView> UpdateAsync(
        Guid id,
        CameraRegistration registration,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var storedCredentials = await repository.GetCredentialsAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var credentials = string.IsNullOrEmpty(registration.Password)
            ? storedCredentials
            : RequireCredentials(registration);

        var endpoint = CreateEndpoint(registration);
        var target = new CameraTarget(endpoint, credentials, NormalizeVendor(registration.VendorKey));
        var (vendorKey, probe) = await probeService.ProbeRawAsync(target, cancellationToken);
        var profiles = MapProfiles(probe);
        var mainProfileId = ResolveMainProfileId(probe, registration.MainProfileId);

        var camera = existing.Update(
            name: ResolveName(registration.Name, vendorKey, probe, fallback: existing.Name),
            host: endpoint.Host.ToString(),
            rtspPort: endpoint.RtspPort,
            vendorKey: vendorKey,
            vendor: probe.Device.Vendor,
            model: probe.Device.Model,
            firmware: probe.Device.Firmware,
            serialNumber: probe.Device.SerialNumber,
            profiles: profiles,
            mainProfileId: mainProfileId,
            now: DateTimeOffset.UtcNow,
            subProfileId: ResolveSubProfileId(
                profiles,
                mainProfileId,
                NormalizeSubProfileId(registration.SubProfileId) ?? existing.SubProfileId));

        var credentialsToSave = string.IsNullOrEmpty(registration.Password) ? null : credentials;
        await repository.UpdateAsync(camera, credentialsToSave, cancellationToken);
        await TryUpsertStreamsAsync(camera, credentials, cancellationToken);

        return CameraView.From(camera);
    }

    /// <summary>Обновляет имя и выбранный субпоток без повторного опроса камеры.</summary>
    public async Task<CameraView> UpdateSettingsAsync(
        Guid id,
        string? name,
        string? subProfileId,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var normalizedName = string.IsNullOrWhiteSpace(name) ? existing.Name : name.Trim();
        var normalizedSub = subProfileId is null
            ? existing.SubProfileId
            : NormalizeSubProfileId(subProfileId);

        if (normalizedSub is not null &&
            !existing.Profiles.Any(profile =>
                string.Equals(profile.Id, normalizedSub, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Выбранный субпоток не найден у камеры.", nameof(subProfileId));
        }

        var camera = existing.UpdateSettings(normalizedName, normalizedSub, DateTimeOffset.UtcNow);
        await repository.UpdateAsync(camera, credentials: null, cancellationToken);

        var credentials = await repository.GetCredentialsAsync(id, cancellationToken);
        if (credentials is not null)
        {
            await TryUpsertStreamsAsync(camera, credentials, cancellationToken);
        }

        return CameraView.From(camera);
    }

    /// <summary>Удаляет камеру из реестра и её потоки из медиа-ядра.</summary>
    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var camera = await repository.FindAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        await TryRemoveStreamsAsync(camera, cancellationToken);
        await repository.RemoveAsync(id, cancellationToken);
    }

    /// <summary>Включает или выключает камеру; потоки заводятся или снимаются соответственно.</summary>
    public async Task<CameraView> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var camera = await repository.SetActiveAsync(id, isActive, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        if (isActive)
        {
            var credentials = await repository.GetCredentialsAsync(id, cancellationToken);
            if (credentials is not null)
            {
                await TryUpsertStreamsAsync(camera, credentials, cancellationToken);
            }
        }
        else
        {
            await TryRemoveStreamsAsync(camera, cancellationToken);
        }

        return CameraView.From(camera);
    }

    /// <summary>JPEG-снапшот профиля сохранённой камеры.</summary>
    public async Task<byte[]> GetSnapshotAsync(
        Guid id,
        CameraStreamProfileKind kind,
        CancellationToken cancellationToken)
    {
        var camera = await repository.FindAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var credentials = await repository.GetCredentialsAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var target = new CameraTarget(CreateEndpoint(camera), credentials, camera.VendorKey);
        return await probeService.GetSnapshotAsync(target, kind, profileId: null, cancellationToken);
    }

    /// <summary>
    /// WHEP-сессия профиля сохранённой камеры: поток регистрируется в медиа-ядре
    /// (с учётными данными) и негоциируется с go2rtc.
    /// </summary>
    public async Task<string> NegotiateWhepAsync(
        Guid id,
        CameraStreamProfileKind kind,
        string sdpOffer,
        CancellationToken cancellationToken,
        string? profileId = null)
    {
        if (string.IsNullOrWhiteSpace(sdpOffer))
        {
            throw new ArgumentException("SDP offer не может быть пустым.", nameof(sdpOffer));
        }

        var camera = await repository.FindAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var credentials = await repository.GetCredentialsAsync(id, cancellationToken)
            ?? throw new CameraNotFoundException(id);

        var isMain = kind == CameraStreamProfileKind.Main;
        var profile = SelectProfile(camera, isMain, profileId);

        var streamId = CameraStreamNames.ForProfile(camera.Id, isMain);
        await mediaEngine.UpsertStreamAsync(
            streamId,
            BuildSources(profile, credentials),
            cancellationToken);

        return await mediaEngine.NegotiateWebRtcAsync(streamId, sdpOffer, cancellationToken);
    }

    /// <summary>Повторно регистрирует потоки всех камер (после запуска или перезапуска медиа-ядра).</summary>
    public async Task SyncStreamsAsync(CancellationToken cancellationToken)
    {
        var cameras = await repository.ListAsync(cancellationToken);

        foreach (var camera in cameras)
        {
            var credentials = await repository.GetCredentialsAsync(camera.Id, cancellationToken);
            if (credentials is null)
            {
                continue;
            }

            try
            {
                await UpsertStreamsAsync(camera, credentials, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Не удалось зарегистрировать потоки камеры {CameraId} в медиа-ядре",
                    camera.Id);
            }
        }
    }

    private async Task TryUpsertStreamsAsync(
        Camera camera,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        try
        {
            await UpsertStreamsAsync(camera, credentials, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Камера {CameraId} сохранена, но её потоки не зарегистрированы в медиа-ядре",
                camera.Id);
        }
    }

    private async Task TryRemoveStreamsAsync(Camera camera, CancellationToken cancellationToken)
    {
        foreach (var streamId in new[]
                 {
                     CameraStreamNames.ForProfile(camera.Id, isMain: true),
                     CameraStreamNames.ForProfile(camera.Id, isMain: false),
                 })
        {
            try
            {
                await mediaEngine.RemoveStreamAsync(streamId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Не удалось удалить поток {StreamId} камеры {CameraId} из медиа-ядра",
                    streamId,
                    camera.Id);
            }
        }
    }

    private async Task UpsertStreamsAsync(
        Camera camera,
        CameraCredentials credentials,
        CancellationToken cancellationToken)
    {
        foreach (var profile in StreamProfiles(camera))
        {
            var isMain = string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal);
            var streamId = CameraStreamNames.ForProfile(camera.Id, isMain);
            await mediaEngine.UpsertStreamAsync(streamId, BuildSources(profile, credentials), cancellationToken);
        }
    }

    /// <summary>Профили, которые публикуются в медиа-ядре: основной и выбранный субпоток.</summary>
    private static IEnumerable<Domain.Cameras.CameraProfile> StreamProfiles(Camera camera)
    {
        var main = camera.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal));
        if (main is not null)
        {
            yield return main;
        }

        if (string.Equals(camera.SubProfileId, camera.MainProfileId, StringComparison.Ordinal))
        {
            yield break;
        }

        var sub = camera.Profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, camera.SubProfileId, StringComparison.Ordinal))
            ?? camera.Profiles.FirstOrDefault(profile =>
                !string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal));

        if (sub is not null)
        {
            yield return sub;
        }
    }

    private static Domain.Cameras.CameraProfile SelectProfile(
        Camera camera,
        bool isMain,
        string? profileId)
    {
        if (isMain)
        {
            return camera.Profiles.FirstOrDefault(profile =>
                       string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal))
                ?? throw MissingProfile(camera, isMain: true);
        }

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            return camera.Profiles.FirstOrDefault(profile =>
                       !string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal) &&
                       string.Equals(profile.Id, profileId, StringComparison.Ordinal))
                ?? throw new CameraOperationNotSupportedException(
                    $"У камеры «{camera.Name}» не найден выбранный субпоток. Обновите настройки камеры.");
        }

        return camera.Profiles.FirstOrDefault(profile =>
                   !string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal) &&
                   string.Equals(profile.Id, camera.SubProfileId, StringComparison.Ordinal))
            ?? camera.Profiles.FirstOrDefault(profile =>
                !string.Equals(profile.Id, camera.MainProfileId, StringComparison.Ordinal))
            ?? throw MissingProfile(camera, isMain: false);
    }

    private static CameraOperationNotSupportedException MissingProfile(Camera camera, bool isMain) =>
        new($"У камеры «{camera.Name}» нет профиля {(isMain ? "основного потока." : "субпотока.")}");

    /// <summary>
    /// Источники потока: RTSP-источник камеры и, при необходимости, ffmpeg-транскод аудио в Opus.
    /// Видео остаётся passthrough из RTSP.
    /// </summary>
    private static IReadOnlyList<string> BuildSources(
        MollyNvr.Domain.Cameras.CameraProfile profile,
        CameraCredentials credentials)
    {
        var sourceUrl = RtspUriFactory.WithCredentials(new Uri(profile.RtspUrl), credentials);
        var audioMode = profile.Audio is not null
            ? AudioPlaybackPolicy.Classify(profile.Audio.AudioCodec)
            : AudioPlaybackMode.Disabled;

        var sources = new List<string> { sourceUrl };

        if (VideoPlaybackPolicy.NeedsTranscode(profile.Video.Codec))
        {
            sources.Add($"ffmpeg:{sourceUrl}#video=h264#hardware");
        }

        if (audioMode == AudioPlaybackMode.Transcoded)
        {
            sources.Add($"ffmpeg:{sourceUrl}#audio=opus");
        }

        return sources;
    }

    private static CameraCredentials RequireCredentials(CameraRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.UserName))
        {
            throw new ArgumentException("Укажите логин камеры.", nameof(registration));
        }

        if (string.IsNullOrEmpty(registration.Password))
        {
            throw new ArgumentException("Укажите пароль камеры.", nameof(registration));
        }

        return new CameraCredentials(registration.UserName.Trim(), registration.Password);
    }

    private static CameraEndpoint CreateEndpoint(CameraRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.Host) ||
            !IPAddress.TryParse(registration.Host.Trim(), out var address))
        {
            throw new ArgumentException("Укажите IP-адрес камеры.", nameof(registration));
        }

        return new CameraEndpoint(address, registration.RtspPort ?? 554);
    }

    private static CameraEndpoint CreateEndpoint(Camera camera)
    {
        if (!IPAddress.TryParse(camera.Host, out var address))
        {
            throw new InvalidOperationException($"В реестре сохранён некорректный адрес камеры «{camera.Host}».");
        }

        return new CameraEndpoint(address, camera.RtspPort);
    }

    private static string? NormalizeVendor(string? vendorKey) =>
        string.IsNullOrWhiteSpace(vendorKey) ? null : vendorKey.Trim().ToLowerInvariant();

    private static string? NormalizeSubProfileId(string? subProfileId) =>
        string.IsNullOrWhiteSpace(subProfileId) ? null : subProfileId.Trim();

    private static string ResolveMainProfileId(CameraProbeResult probe, string? requestedMainProfileId)
    {
        var normalized = NormalizeProfileId(requestedMainProfileId);

        if (normalized is not null &&
            probe.Profiles.Any(profile => string.Equals(profile.Id, normalized, StringComparison.Ordinal)))
        {
            return normalized;
        }

        return probe.MainProfile?.Id
            ?? throw new CameraOperationNotSupportedException("Модуль камеры не вернул основной профиль потока.");
    }

    private static string? NormalizeProfileId(string? profileId) =>
        string.IsNullOrWhiteSpace(profileId) ? null : profileId.Trim();

    private static string ResolveSubProfileId(
        IReadOnlyList<Domain.Cameras.CameraProfile> profiles,
        string mainProfileId,
        string? requestedSubProfileId)
    {
        if (!string.IsNullOrWhiteSpace(requestedSubProfileId) &&
            !string.Equals(requestedSubProfileId, mainProfileId, StringComparison.Ordinal) &&
            profiles.Any(profile => string.Equals(profile.Id, requestedSubProfileId, StringComparison.Ordinal)))
        {
            return requestedSubProfileId;
        }

        return profiles.FirstOrDefault(profile =>
            !string.Equals(profile.Id, mainProfileId, StringComparison.Ordinal))?.Id ?? mainProfileId;
    }

    private static string ResolveName(
        string? name,
        string vendorKey,
        CameraProbeResult probe,
        string? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        if (fallback is not null)
        {
            return fallback;
        }

        if (!string.IsNullOrWhiteSpace(probe.Device.DeviceName))
        {
            return probe.Device.DeviceName.Trim();
        }

        var model = $"{probe.Device.Vendor} {probe.Device.Model}".Trim();
        return string.IsNullOrWhiteSpace(model) ? vendorKey : model;
    }

    private static IReadOnlyList<Domain.Cameras.CameraProfile> MapProfiles(CameraProbeResult probe) =>
        probe.Profiles
            .Select(static profile => new Domain.Cameras.CameraProfile(
                Id: profile.Id,
                Name: profile.DisplayName,
                Video: new Domain.Cameras.CameraProfileVideo(
                    Codec: profile.Video.Codec,
                    Width: profile.Video.Width,
                    Height: profile.Video.Height,
                    FrameRate: profile.Video.FrameRate,
                    BitrateKbps: profile.Video.BitrateKbps),
                Audio: profile.Audio.Enabled
                    ? new Domain.Cameras.CameraProfileAudio(
                        AudioCodec: profile.Audio.Codec ?? "Unknown",
                        AudioSampleRate: profile.Audio.SampleRate ?? 0,
                        BitrateKbps: profile.Audio.Bitrate ?? 0)
                    : null,
                RtspUrl: profile.RtspUrl))
            .ToList();
}
