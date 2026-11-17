using Microsoft.Extensions.Options;
using MollyNvr.Application.Discovery;
using MollyNvr.Infrastructure.Discovery;

namespace MollyNvr.Api.Endpoints;

/// <summary>Поиск камер в локальной сети: multicast, подсеть и отдельный адрес.</summary>
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

    /// <summary>Параметры автопоиска для мастера добавления камеры.</summary>
    /// <remarks>Ответ: <c>{ "defaultRange": "192.168.1.0/24" }</c>.</remarks>
    private static IResult GetSettings(IOptions<DiscoveryOptions> options) =>
        Results.Ok(new { defaultRange = options.Value.DefaultRange });

    /// <summary>Сканирует подсеть или диапазон адресов типовыми портами камер.</summary>
    /// <remarks>
    /// Тело — <see cref="DiscoveryScanRequest"/>; пустой <c>range</c> — диапазон по умолчанию из конфигурации.
    /// Поддерживаются CIDR и диапазоны: <c>192.168.1.0/24</c>, <c>192.168.1.10-192.168.1.50</c>,
    /// <c>192.168.1.10-50</c>, одиночный адрес. Максимум 1024 адреса.
    /// Ответы: 200 — список устройств; 400 — некорректный диапазон.
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

    /// <summary>Ищет камеры multicast-протоколами: WS-Discovery, SSDP, mDNS, Hikvision SADP.</summary>
    /// <remarks>
    /// В bridge-сети Docker multicast из LAN может не доходить до контейнера — ответ будет пустым.
    /// Ответ: <c>{ "devices": [...] }</c>.
    /// </remarks>
    private static async Task<IResult> ScanMulticastAsync(
        DiscoveryService service,
        CancellationToken cancellationToken)
    {
        var devices = await service.ScanMulticastAsync(cancellationToken);
        return Results.Ok(new { devices });
    }

    /// <summary>Проверяет конкретный IPv4-адрес и возвращает найденное устройство.</summary>
    /// <remarks>
    /// Тело — <see cref="DiscoveryAddressRequest"/>. <c>vendor</c> — необязательный ключ модуля.
    /// Ответы: 200 — <c>{ "devices": [...] }</c>; 400 — некорректный адрес или модуль.
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

/// <summary>Параметры сканирования подсети.</summary>
/// <param name="Range">CIDR, диапазон адресов или одиночный IP; пусто — значение по умолчанию.</param>
public sealed record DiscoveryScanRequest(string? Range);

/// <summary>Параметры проверки одного адреса.</summary>
/// <param name="Host">IPv4-адрес устройства.</param>
/// <param name="Vendor">Ключ модуля производителя; пусто — определить автоматически.</param>
public sealed record DiscoveryAddressRequest(string? Host, string? Vendor);
