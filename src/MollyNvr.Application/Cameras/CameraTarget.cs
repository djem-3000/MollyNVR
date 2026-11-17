using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>
/// Цель опроса камеры: сетевой адрес, учётные данные и (опционально) явно выбранный модуль производителя.
/// </summary>
public sealed record CameraTarget(CameraEndpoint Endpoint, CameraCredentials Credentials, string? VendorKey = null);
