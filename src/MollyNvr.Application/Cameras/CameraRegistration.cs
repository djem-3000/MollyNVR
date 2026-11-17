namespace MollyNvr.Application.Cameras;

/// <summary>Параметры добавления или изменения камеры (пароль не возвращается наружу).</summary>
public sealed record CameraRegistration(
    string? Name,
    string? Host,
    int? RtspPort,
    string? UserName,
    string? Password,
    string? VendorKey,
    string? SubProfileId = null,
    string? MainProfileId = null);
