using System.Net;
using System.Runtime.CompilerServices;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>
/// Сценарии работы с камерами: опрос модулем производителя и снапшот.
/// В M2 камеры не сохраняются — параметры подключения передаются в каждом запросе.
/// </summary>
public sealed class CameraProbeService(
    IEnumerable<ICameraModule> modules,
    IRtspMediaProbe mediaProbe,
    IFrameGrabber frameGrabber,
    IPortScanner portScanner)
{
    private readonly IReadOnlyList<ICameraModule> _modules = modules.ToList();

    private const string ReolinkVendorKey = "reolink";

    private static readonly string[] ReolinkProbeOrder = ["onvif", ReolinkVendorKey];

    /// <summary>Ключи зарегистрированных модулей производителей.</summary>
    public IReadOnlyList<string> ModuleKeys => _modules.Select(static module => module.Key).ToList();

    /// <summary>Опрашивает камеру, определяя модуль производителя автоматически (или по подсказке).</summary>
    public async Task<CameraProbeView> ProbeAsync(CameraTarget target, CancellationToken cancellationToken)
    {
        var (vendorKey, result) = await ProbeRawAsync(target, cancellationToken);
        var openPorts = await FindOpenPortsAsync(target.Endpoint.Address, cancellationToken);
        return CameraProbeView.From(vendorKey, result, openPorts);
    }

    /// <summary>Опрашивает камеру и возвращает сырые профили (URL без кредов) — для сохранения в реестр.</summary>
    public async Task<(string VendorKey, CameraProbeResult Result)> ProbeRawAsync(
        CameraTarget target,
        CancellationToken cancellationToken)
    {
        var (module, result) = await ProbeWithModuleAsync(target, cancellationToken);
        return (module.Key, result);
    }

    /// <summary>Сканирует типовые порты устройства; ошибки сканирования не прерывают опрос.</summary>
    private async Task<IReadOnlyList<int>> FindOpenPortsAsync(
        IPAddress host,
        CancellationToken cancellationToken)
    {
        try
        {
            return await portScanner.FindOpenPortsAsync(host, CameraPortCatalog.ScanPorts, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Запрашивает у камеры состояние DHCP. Если модуль или камера не сообщают параметр,
    /// возвращает <c>null</c>.
    /// </summary>
    public async Task<bool?> GetDhcpEnabledAsync(CameraTarget target, CancellationToken cancellationToken)
    {
        var candidates = ResolveCandidates(target.VendorKey);

        foreach (var module in candidates)
        {
            if (module is not ICameraNetworkInfoProvider provider)
            {
                continue;
            }

            if (target.VendorKey is null && !await CanHandleAsync(module, target.Endpoint, cancellationToken))
            {
                continue;
            }

            try
            {
                return await provider.GetDhcpEnabledAsync(
                    target.Endpoint,
                    target.Credentials,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Возвращает JPEG-снапшот: нативный метод модуля, иначе — кадр из RTSP.</summary>
    public async Task<byte[]> GetSnapshotAsync(
        CameraTarget target,
        CameraStreamProfileKind kind,
        string? profileId,
        CancellationToken cancellationToken)
    {
        var (module, result) = await ProbeWithModuleAsync(target, cancellationToken);
        var profile = SelectProfile(result, kind, profileId);

        try
        {
            var native = await module.GetSnapshotAsync(target.Endpoint, target.Credentials, profile, cancellationToken);
            if (native.Length > 0)
            {
                return native;
            }
        }
        catch (CameraModuleException)
        {
            // Камера не умеет нативный снапшот (например, HTTP-порт закрыт) — берём кадр из RTSP.
        }

        var sourceUrl = RtspUriFactory.WithCredentials(new Uri(profile.RtspUrl), target.Credentials);
        return await frameGrabber.GrabJpegAsync(sourceUrl, cancellationToken);
    }

    /// <summary>Типы событий, которые камера сообщает; пустой список — источник недоступен.</summary>
    public async Task<IReadOnlyList<CameraEventType>> GetEventTypesAsync(
        CameraTarget target,
        CancellationToken cancellationToken)
    {
        var source = ResolveEventSource(target.VendorKey);
        if (source is null)
        {
            return [];
        }

        try
        {
            return await source.GetEventTypesAsync(target.Endpoint, target.Credentials, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Поток событий камеры; пустой, если модуль не умеет события.</summary>
    public async IAsyncEnumerable<CameraEvent> WatchEventsAsync(
        CameraTarget target,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var source = ResolveEventSource(target.VendorKey);
        if (source is null)
        {
            yield break;
        }

        await foreach (var cameraEvent in source
                           .WatchEventsAsync(target.Endpoint, target.Credentials, cancellationToken)
                           .WithCancellation(cancellationToken))
        {
            yield return cameraEvent;
        }
    }

    private ICameraEventSource? ResolveEventSource(string? vendorKey)
    {
        IReadOnlyList<ICameraModule> candidates;

        try
        {
            candidates = ResolveCandidates(vendorKey);
        }
        catch (CameraOperationNotSupportedException)
        {
            return null;
        }

        return candidates.OfType<ICameraEventSource>().FirstOrDefault();
    }

    private async Task<(ICameraModule Module, CameraProbeResult Result)> ProbeWithModuleAsync(
        CameraTarget target,
        CancellationToken cancellationToken)
    {
        var candidates = ResolveCandidates(target.VendorKey);

        CameraAuthenticationException? authenticationError = null;
        var errors = new List<CameraModuleException>();

        foreach (var module in candidates)
        {
            try
            {
                if (target.VendorKey is null && !await CanHandleAsync(module, target.Endpoint, cancellationToken))
                {
                    continue;
                }

                var result = await module.ProbeAsync(target.Endpoint, target.Credentials, cancellationToken);
                return (module, await EnrichWithMediaAsync(result, target, cancellationToken));
            }
            catch (CameraAuthenticationException exception)
            {
                authenticationError ??= exception;
            }
            catch (CameraModuleException exception)
            {
                errors.Add(exception);
            }
        }

        if (authenticationError is not null)
        {
            throw authenticationError;
        }

        throw new CameraUnavailableException(
            $"Камера {target.Endpoint.Host} не ответила ни одному из поддерживаемых протоколов.",
            errors.Count == 1 ? errors[0] : null);
    }

    private IReadOnlyList<ICameraModule> ResolveCandidates(string? vendorKey)
    {
        if (vendorKey is null)
        {
            return _modules;
        }

        // Reolink: сначала стандартный ONVIF, затем проприетарный Baichuan через мост.
        var keys = string.Equals(vendorKey, ReolinkVendorKey, StringComparison.OrdinalIgnoreCase)
            ? ReolinkProbeOrder
            : [vendorKey];

        var modules = keys
            .Select(key => _modules.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase)))
            .OfType<ICameraModule>()
            .ToList();

        if (modules.Count == 0)
        {
            throw new CameraOperationNotSupportedException(
                $"Модуль производителя «{vendorKey}» не поддерживается. Доступны: {string.Join(", ", ModuleKeys)}.");
        }

        return modules;
    }

    private static async Task<bool> CanHandleAsync(
        ICameraModule module,
        CameraEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            return await module.CanHandleAsync(endpoint, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<CameraProbeResult> EnrichWithMediaAsync(
        CameraProbeResult result,
        CameraTarget target,
        CancellationToken cancellationToken)
    {
        var profiles = new List<CameraProfile>(result.Profiles.Count);

        foreach (var profile in result.Profiles)
        {
            var media = await ProbeMediaAsync(profile, target, cancellationToken);
            profiles.Add(media is null ? profile : ApplyMedia(profile, media));
        }

        return result with { Profiles = profiles };
    }

    private async Task<RtspMediaInfo?> ProbeMediaAsync(
        CameraProfile profile,
        CameraTarget target,
        CancellationToken cancellationToken)
    {
        try
        {
            return await mediaProbe.ProbeAsync(new Uri(profile.RtspUrl), target.Credentials, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static CameraProfile ApplyMedia(CameraProfile profile, RtspMediaInfo media)
    {
        var video = media.VideoCodec is null
            ? profile.Video
            : profile.Video with { Codec = media.VideoCodec };

        var audio = profile.Audio;
        if (media.AudioCodec is not null)
        {
            audio = audio with { Enabled = true, Codec = media.AudioCodec };
        }

        if (media.AudioSampleRate is not null)
        {
            audio = audio with { Enabled = true, SampleRate = media.AudioSampleRate };
        }

        return profile with { Video = video, Audio = audio };
    }

    private static CameraProfile SelectProfile(
        CameraProbeResult result,
        CameraStreamProfileKind kind,
        string? profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            return result.Profiles.FirstOrDefault(profile =>
                       string.Equals(profile.Id, profileId, StringComparison.Ordinal))
                   ?? throw new CameraOperationNotSupportedException(
                       $"Профиль «{profileId}» не найден у камеры.");
        }

        return (kind == CameraStreamProfileKind.Main ? result.MainProfile : result.SubProfile)
            ?? throw new CameraOperationNotSupportedException(
                $"У камеры нет профиля {(kind == CameraStreamProfileKind.Main ? "основного потока" : "субпотока")}.");
    }
}
