using System.Net;
using System.Runtime.CompilerServices;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>
/// Camera scenarios: probing with a vendor module and taking a snapshot.
/// Cameras are not stored here — connection parameters are supplied with every request.
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

        /// <summary>Keys of the registered vendor modules.</summary>
    public IReadOnlyList<string> ModuleKeys => _modules.Select(static module => module.Key).ToList();

        /// <summary>Probes the camera, detecting the vendor module automatically (or from the hint).</summary>
    public async Task<CameraProbeView> ProbeAsync(CameraTarget target, CancellationToken cancellationToken)
    {
        var (vendorKey, result) = await ProbeRawAsync(target, cancellationToken);
        var openPorts = await FindOpenPortsAsync(target.Endpoint.Address, cancellationToken);
        return CameraProbeView.From(vendorKey, result, openPorts);
    }

        /// <summary>Probes the camera and returns raw profiles (credential-free URLs) for the registry.</summary>
    public async Task<(string VendorKey, CameraProbeResult Result)> ProbeRawAsync(
        CameraTarget target,
        CancellationToken cancellationToken)
    {
        var (module, result) = await ProbeWithModuleAsync(target, cancellationToken);
        return (module.Key, result);
    }

        /// <summary>Scans the typical device ports; scan errors do not interrupt the probe.</summary>
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
        /// Requests the DHCP state from the camera. When the module or the camera does not report
        /// the parameter, returns <c>null</c>.
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

        /// <summary>Returns a JPEG snapshot: the module's native method, otherwise a frame from RTSP.</summary>
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
                        // The camera has no native snapshot (for example, the HTTP port is closed) — take a frame from RTSP.
        }

        var sourceUrl = RtspUriFactory.WithCredentials(new Uri(profile.RtspUrl), target.Credentials);
        return await frameGrabber.GrabJpegAsync(sourceUrl, cancellationToken);
    }

        /// <summary>Event types reported by the camera; an empty list means the source is unavailable.</summary>
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

        /// <summary>Camera event stream; empty when the module does not support events.</summary>
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
                        $"Camera {target.Endpoint.Host} did not respond over any supported protocol.",
            errors.Count == 1 ? errors[0] : null);
    }

    private IReadOnlyList<ICameraModule> ResolveCandidates(string? vendorKey)
    {
        if (vendorKey is null)
        {
            return _modules;
        }

                // Reolink: standard ONVIF first, then proprietary Baichuan through the bridge.
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
                                $"Vendor module '{vendorKey}' is not supported. Available: {string.Join(", ", ModuleKeys)}.");
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
                                              $"Profile '{profileId}' was not found on the camera.");
        }

        return (kind == CameraStreamProfileKind.Main ? result.MainProfile : result.SubProfile)
            ?? throw new CameraOperationNotSupportedException(
                                $"The camera has no {(kind == CameraStreamProfileKind.Main ? "main stream" : "substream")} profile.");
    }
}
