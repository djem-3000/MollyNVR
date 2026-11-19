using System.Net;
using System.Text;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Streams;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Api.Endpoints;

/// <summary>
/// Camera operations: device discovery probe, snapshot, and the registry of saved cameras
/// (add, update, delete).
/// </summary>
public static class CameraEndpoints
{
    public static IEndpointRouteBuilder MapCameraEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cameras");

        group.MapGet("/modules", ListModules);
        group.MapPost("/probe", ProbeAsync);
        group.MapPost("/snapshot", SnapshotAsync);

        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", FindAsync);
        group.MapGet("/{id:guid}/network", GetNetworkAsync);
        group.MapPost("", AddAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPut("/{id:guid}/settings", UpdateSettingsAsync);
        group.MapDelete("/{id:guid}", RemoveAsync);
        group.MapPut("/{id:guid}/active", SetActiveAsync);
        group.MapGet("/{id:guid}/snapshot", SavedSnapshotAsync);
        group.MapPost("/{id:guid}/whep", NegotiateWhepAsync);

        return group;
    }

        /// <summary>List of registered vendor modules.</summary>
        /// <remarks>Response: <c>{ "modules": ["hikvision", "onvif"] }</c>.</remarks>
    private static IResult ListModules(CameraProbeService service) =>
        Results.Ok(new { modules = service.ModuleKeys });

        /// <summary>Probes the camera and returns device details and main/sub profiles.</summary>
    /// <remarks>
        /// Body is <see cref="CameraRequest"/>. The vendor module is detected automatically (or from the <c>vendor</c> field).
        /// Profile codecs are verified against the SDP of the real RTSP stream.
        /// Responses: 200 — camera details; 400 — invalid parameters; 401 — invalid credentials;
        /// 422 — the device does not support the operation; 502 — the camera is unreachable.
    /// </remarks>
    private static async Task<IResult> ProbeAsync(
        CameraRequest request,
        CameraProbeService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var target = request.ToTarget();
            return Results.Ok(await service.ProbeAsync(target, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (CameraModuleException exception)
        {
            return ToProblem(exception);
        }
    }

        /// <summary>Returns a JPEG snapshot of the selected camera profile.</summary>
    /// <remarks>
        /// Body is <see cref="CameraRequest"/> with the <c>profile</c> field (<c>main</c> or <c>sub</c>).
        /// The native camera snapshot is used first; when it is unavailable, a frame from RTSP is taken through the media engine.
        /// Responses: 200 — <c>image/jpeg</c>; 400 — invalid parameters; 401 — invalid credentials;
        /// 422 — the operation is not supported; 502 — the camera is unreachable.
    /// </remarks>
    private static async Task<IResult> SnapshotAsync(
        CameraRequest request,
        CameraProbeService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var target = request.ToTarget();
            var profile = request.ProfileKind();
            var jpeg = await service.GetSnapshotAsync(target, profile, request.ProfileId, cancellationToken);
            return Results.File(jpeg, "image/jpeg");
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (CameraModuleException exception)
        {
            return ToProblem(exception);
        }
    }

        /// <summary>Changes the camera name and the selected substream without probing the device again.</summary>
    /// <remarks>
        /// Body is <see cref="CameraSettingsRequest"/>. An empty <c>subProfileId</c> keeps the previous selection.
        /// Responses: 200 — settings saved; 400 — unknown substream;
        /// 404 — camera not found.
    /// </remarks>
    private static async Task<IResult> UpdateSettingsAsync(
        Guid id,
        CameraSettingsRequest request,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            var camera = await registry.UpdateSettingsAsync(
                id,
                request.Name,
                request.SubProfileId,
                cancellationToken);
            return Results.Ok(camera);
        }
        catch (CameraNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

        /// <summary>List of saved cameras with stream profiles.</summary>
        /// <remarks>Passwords are never returned; credentials in RTSP URLs are masked.</remarks>
    private static async Task<IResult> ListAsync(CameraRegistry registry, CancellationToken cancellationToken) =>
        Results.Ok(await registry.ListAsync(cancellationToken));

        /// <summary>A saved camera by identifier.</summary>
        /// <remarks>Responses: 200 — camera; 404 — camera not found.</remarks>
    private static async Task<IResult> FindAsync(
        Guid id,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        var camera = await registry.FindAsync(id, cancellationToken);
        return camera is null
                        ? Results.Problem($"Camera {id} not found.", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(camera);
    }

        /// <summary>Returns network parameters of a saved camera (currently the DHCP state).</summary>
    /// <remarks>
        /// A value of <c>dhcpEnabled = null</c> means the camera did not report the parameter.
        /// Responses: 200 — parameters (including an unknown value); 404 — camera not found.
    /// </remarks>
    private static async Task<IResult> GetNetworkAsync(
        Guid id,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            var dhcpEnabled = await registry.GetDhcpEnabledAsync(id, cancellationToken);
            return Results.Ok(new { dhcpEnabled });
        }
        catch (CameraNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

        /// <summary>Verifies the connection and adds the camera to the registry.</summary>
    /// <remarks>
        /// Body is <see cref="CameraRegistrationRequest"/>. A full camera probe runs before saving,
        /// main/sub profiles are stored in the database, and streams are registered in the media engine.
        /// Responses: 201 — camera added; 400 — invalid parameters; 401 — invalid credentials;
        /// 422 — the device does not support the operation; 502 — the camera is unreachable.
    /// </remarks>
    private static async Task<IResult> AddAsync(
        CameraRegistrationRequest request,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            var camera = await registry.AddAsync(request.ToRegistration(), cancellationToken);
            return Results.Created($"/api/cameras/{camera.Id}", camera);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (CameraModuleException exception)
        {
            return ToProblem(exception);
        }
    }

        /// <summary>Probes the camera again and refreshes its details and profiles.</summary>
    /// <remarks>
        /// Body is <see cref="CameraRegistrationRequest"/>. An empty password means "keep the current one".
        /// Responses: 200 — camera updated; 400 — invalid parameters; 401 — invalid credentials;
        /// 404 — camera not found; 422 — the device does not support the operation; 502 — the camera is unreachable.
    /// </remarks>
    private static async Task<IResult> UpdateAsync(
        Guid id,
        CameraRegistrationRequest request,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await registry.UpdateAsync(id, request.ToRegistration(), cancellationToken));
        }
        catch (CameraNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (CameraModuleException exception)
        {
            return ToProblem(exception);
        }
    }

        /// <summary>Removes the camera from the registry and its streams from the media engine.</summary>
        /// <remarks>Responses: 204 — camera removed; 404 — camera not found.</remarks>
    private static async Task<IResult> RemoveAsync(
        Guid id,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            await registry.RemoveAsync(id, cancellationToken);
            return Results.NoContent();
        }
        catch (CameraNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

        /// <summary>Enables or disables the camera (inactive cameras are hidden in the live grid).</summary>
    /// <remarks>
        /// Body is <see cref="CameraActiveRequest"/>. On disable, the camera streams are removed from the media engine,
        /// on enable they are registered again.
        /// Responses: 200 — camera updated; 404 — camera not found.
    /// </remarks>
    private static async Task<IResult> SetActiveAsync(
        Guid id,
        CameraActiveRequest request,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await registry.SetActiveAsync(id, request.Active, cancellationToken));
        }
        catch (CameraNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

        /// <summary>JPEG snapshot of a saved camera profile.</summary>
    /// <remarks>
        /// The profile is set by the <c>profile</c> query string (<c>main</c> or <c>sub</c>).
        /// Responses: 200 — <c>image/jpeg</c>; 400 — invalid profile; 404 — camera not found;
        /// 401/422/502 — camera connection errors.
    /// </remarks>
    private static async Task<IResult> SavedSnapshotAsync(
        Guid id,
        string? profile,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            var kind = ParseProfile(profile);
            var jpeg = await registry.GetSnapshotAsync(id, kind, cancellationToken);
            return Results.File(jpeg, "image/jpeg");
        }
        catch (CameraNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (CameraModuleException exception)
        {
            return ToProblem(exception);
        }
    }

        /// <summary>WHEP proxy for a saved camera: substream for tiles, main stream for fullscreen.</summary>
    /// <remarks>
        /// The profile is set by the <c>profile</c> query string (<c>sub</c> by default, <c>main</c> for fullscreen).
        /// A specific substream can be selected with the <c>profileId</c> query string; otherwise the substream
        /// saved in the camera settings is used. The request body is an SDP offer, the response is the go2rtc SDP answer.
        /// The stream is registered in the media engine with the camera credentials beforehand.
        /// Responses: 200 — session established; 400 — empty offer or unknown profile;
        /// 404 — camera not found; 422 — the camera has no requested profile; 502 — media engine error.
    /// </remarks>
    private static async Task<IResult> NegotiateWhepAsync(
        Guid id,
        string? profile,
        string? profileId,
        HttpRequest request,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var offer = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(offer))
        {
                        return Results.BadRequest(new { error = "The request body must contain an SDP offer." });
        }

        try
        {
            var kind = ParseProfile(profile);
            var answer = await registry.NegotiateWhepAsync(id, kind, offer, cancellationToken, profileId);
            return Results.Text(answer, "application/sdp");
        }
        catch (CameraNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (CameraOperationNotSupportedException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (MediaEngineException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static CameraStreamProfileKind ParseProfile(string? profile) => profile?.Trim().ToLowerInvariant() switch
    {
        null or "" or "main" => CameraStreamProfileKind.Main,
        "sub" => CameraStreamProfileKind.Sub,
                var other => throw new ArgumentException($"Unknown profile '{other}': expected main or sub.", nameof(profile)),
    };

    private static IResult ToProblem(CameraModuleException exception) => exception switch
    {
        CameraAuthenticationException => Results.Problem(exception.Message, statusCode: StatusCodes.Status401Unauthorized),
        CameraOperationNotSupportedException => Results.Problem(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        _ => Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway),
    };
}

/// <summary>Camera connection parameters for the <c>/api/cameras</c> endpoints.</summary>
/// <param name="Host">Camera IP address or host name.</param>
/// <param name="RtspPort">RTSP port (554 by default).</param>
/// <param name="UserName">Camera account login.</param>
/// <param name="Password">Camera account password (never returned to clients).</param>
/// <param name="Vendor">Vendor module hint (<c>hikvision</c>, <c>onvif</c>); empty — autodetect.</param>
/// <param name="Profile">Stream profile for the snapshot: <c>main</c> or <c>sub</c>.</param>
/// <param name="ProfileId">Exact profile identifier for the snapshot; takes precedence over <c>profile</c>.</param>
public sealed record CameraRequest(
    string? Host,
    int? RtspPort,
    string? UserName,
    string? Password,
    string? Vendor,
    string? Profile,
    string? ProfileId = null)
{
        /// <summary>Validates the parameters and creates the probe target.</summary>
    public CameraTarget ToTarget()
    {
        var address = ParseHost(Host);

        if (string.IsNullOrWhiteSpace(UserName))
        {
                        throw new ArgumentException("Specify the camera login.", nameof(UserName));
        }

        if (string.IsNullOrEmpty(Password))
        {
                        throw new ArgumentException("Specify the camera password.", nameof(Password));
        }

        var endpoint = new CameraEndpoint(address, RtspPort ?? 554);

        var credentials = new CameraCredentials(UserName, Password);
        var vendor = string.IsNullOrWhiteSpace(Vendor) ? null : Vendor.Trim().ToLowerInvariant();

        return new CameraTarget(endpoint, credentials, vendor);
    }

        /// <summary>Stream profile from the <c>profile</c> field (main by default).</summary>
    public CameraStreamProfileKind ProfileKind() => Profile?.Trim().ToLowerInvariant() switch
    {
        null or "" or "main" => CameraStreamProfileKind.Main,
        "sub" => CameraStreamProfileKind.Sub,
                var other => throw new ArgumentException($"Unknown profile '{other}': expected main or sub.", nameof(Profile)),
    };

    private static IPAddress ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
                        throw new ArgumentException("Specify the camera IP address or host name.", nameof(Host));
        }

        if (IPAddress.TryParse(host.Trim(), out var address))
        {
            return address;
        }

                throw new ArgumentException($"'{host}' is not a valid camera IP address.", nameof(Host));
    }
}

/// <summary>Parameters for adding or updating a saved camera.</summary>
/// <param name="Name">Display name; empty — device name or "vendor model".</param>
/// <param name="Host">Camera IP address.</param>
/// <param name="RtspPort">RTSP port (554 by default).</param>
/// <param name="UserName">Camera account login.</param>
/// <param name="Password">Password; on update, an empty value means "keep the current one".</param>
/// <param name="Vendor">Vendor module hint (<c>hikvision</c>, <c>onvif</c>); empty — autodetect.</param>
/// <param name="SubProfileId">Identifier of the selected substream; empty — the first substream.</param>
/// <param name="MainProfileId">Identifier of the main profile; empty — the profile chosen by the module.</param>
public sealed record CameraRegistrationRequest(
    string? Name,
    string? Host,
    int? RtspPort,
    string? UserName,
    string? Password,
    string? Vendor,
    string? SubProfileId = null,
    string? MainProfileId = null)
{
        /// <summary>Maps request parameters to the camera registry scenario.</summary>
    public CameraRegistration ToRegistration() =>
        new(Name, Host, RtspPort, UserName, Password, Vendor, SubProfileId, MainProfileId);
}

/// <summary>Saved camera settings that do not require probing the device again.</summary>
/// <param name="Name">New display name; empty — keep the current one.</param>
/// <param name="SubProfileId">Substream identifier; empty — keep the current one.</param>
public sealed record CameraSettingsRequest(string? Name, string? SubProfileId);

/// <summary>Active flag of a saved camera.</summary>
/// <param name="Active"><c>true</c> — show in the live grid, <c>false</c> — hide.</param>
public sealed record CameraActiveRequest(bool Active);
