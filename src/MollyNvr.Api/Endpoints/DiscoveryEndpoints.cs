using Microsoft.Extensions.Options;
using MollyNvr.Application.Discovery;
using MollyNvr.Infrastructure.Discovery;

namespace MollyNvr.Api.Endpoints;

/// <summary>Camera discovery on the local network: multicast, subnet and a single address.</summary>
public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/discovery");

        group.MapGet("/settings", GetSettings);
        group.MapPost("/scan", ScanUnicastAsync);
        group.MapPost("/multicast", ScanMulticastAsync);
        group.MapPost("/ip", ScanAddressAsync);

        return group;
    }

        /// <summary>Discovery parameters for the add-camera wizard.</summary>
        /// <remarks>Response: <c>{ "defaultRange": "192.168.1.0/24" }</c>.</remarks>
    private static IResult GetSettings(IOptions<DiscoveryOptions> options) =>
        Results.Ok(new { defaultRange = options.Value.DefaultRange });

        /// <summary>Scans a subnet or address range with typical camera ports.</summary>
    /// <remarks>
        /// Body is <see cref="DiscoveryScanRequest"/>; an empty <c>range</c> uses the default range from configuration.
        /// CIDR and ranges are supported: <c>192.168.1.0/24</c>, <c>192.168.1.10-192.168.1.50</c>,
        /// <c>192.168.1.10-50</c>, a single address. Up to 1024 addresses.
        /// Responses: 200 — device list; 400 — invalid range.
    /// </remarks>
    private static async Task<IResult> ScanUnicastAsync(
        DiscoveryScanRequest request,
        DiscoveryService service,
        IOptions<DiscoveryOptions> options,
        CancellationToken cancellationToken)
    {
        var range = string.IsNullOrWhiteSpace(request.Range) ? options.Value.DefaultRange : request.Range;

        try
        {
            var devices = await service.ScanUnicastAsync(range, cancellationToken);
            return Results.Ok(new { range, devices });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

        /// <summary>Finds cameras with multicast protocols: WS-Discovery, SSDP, mDNS, Hikvision SADP.</summary>
    /// <remarks>
        /// In a Docker bridge network, multicast from the LAN may not reach the container — the result will be empty.
        /// Response: <c>{ "devices": [...] }</c>.
    /// </remarks>
    private static async Task<IResult> ScanMulticastAsync(
        DiscoveryService service,
        CancellationToken cancellationToken)
    {
        var devices = await service.ScanMulticastAsync(cancellationToken);
        return Results.Ok(new { devices });
    }

        /// <summary>Checks a single IPv4 address and returns the discovered device.</summary>
    /// <remarks>
        /// Body is <see cref="DiscoveryAddressRequest"/>. <c>vendor</c> is an optional module key.
        /// Responses: 200 — <c>{ "devices": [...] }</c>; 400 — invalid address or module.
    /// </remarks>
    private static async Task<IResult> ScanAddressAsync(
        DiscoveryAddressRequest request,
        DiscoveryService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var devices = await service.ScanAddressAsync(request.Host, request.Vendor, cancellationToken);
            return Results.Ok(new { devices });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}

/// <summary>Subnet scan parameters.</summary>
/// <param name="Range">CIDR, address range or a single IP; empty — the default value.</param>
public sealed record DiscoveryScanRequest(string? Range);

/// <summary>Parameters for checking a single address.</summary>
/// <param name="Host">IPv4 address of the device.</param>
/// <param name="Vendor">Vendor module key; empty — detect automatically.</param>
public sealed record DiscoveryAddressRequest(string? Host, string? Vendor);
