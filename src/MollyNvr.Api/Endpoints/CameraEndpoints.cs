using System.Net;
using System.Text;
using MollyNvr.Application.Cameras;
using MollyNvr.Application.Streams;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Api.Endpoints;

/// <summary>
/// Работа с камерами: автопоиск-опрос устройства, снапшот, а также реестр сохранённых камер
/// (добавление, изменение, удаление).
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

    /// <summary>Список зарегистрированных модулей производителей.</summary>
    /// <remarks>Ответ: <c>{ "modules": ["hikvision", "onvif"] }</c>.</remarks>
    private static IResult ListModules(CameraProbeService service) =>
        Results.Ok(new { modules = service.ModuleKeys });

    /// <summary>Опрашивает камеру и возвращает сведения об устройстве и профили main/sub.</summary>
    /// <remarks>
    /// Тело — <see cref="CameraRequest"/>. Модуль производителя определяется автоматически (или полем <c>vendor</c>).
    /// Кодеки профилей уточняются по SDP реального RTSP-потока.
    /// Ответы: 200 — данные камеры; 400 — некорректные параметры; 401 — неверные учётные данные;
    /// 422 — устройство не поддерживает операцию; 502 — камера недоступна.
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

    /// <summary>Возвращает JPEG-снапшот выбранного профиля камеры.</summary>
    /// <remarks>
    /// Тело — <see cref="CameraRequest"/> с полем <c>profile</c> (<c>main</c> или <c>sub</c>).
    /// Сначала используется нативный снапшот камеры, при его отсутствии — кадр из RTSP через медиа-ядро.
    /// Ответы: 200 — <c>image/jpeg</c>; 400 — некорректные параметры; 401 — неверные учётные данные;
    /// 422 — операция не поддерживается; 502 — камера недоступна.
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

    /// <summary>Меняет имя камеры и выбранный субпоток без повторного опроса устройства.</summary>
    /// <remarks>
    /// Тело — <see cref="CameraSettingsRequest"/>. Пустой <c>subProfileId</c> оставляет прежний выбор.
    /// Ответы: 200 — настройки сохранены; 400 — неизвестный субпоток;
    /// 404 — камера не найдена.
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

    /// <summary>Список сохранённых камер с профилями потоков.</summary>
    /// <remarks>Пароли не возвращаются; в RTSP-URL учётные данные маскируются.</remarks>
    private static async Task<IResult> ListAsync(CameraRegistry registry, CancellationToken cancellationToken) =>
        Results.Ok(await registry.ListAsync(cancellationToken));

    /// <summary>Сохранённая камера по идентификатору.</summary>
    /// <remarks>Ответы: 200 — камера; 404 — камера не найдена.</remarks>
    private static async Task<IResult> FindAsync(
        Guid id,
        CameraRegistry registry,
        CancellationToken cancellationToken)
    {
        var camera = await registry.FindAsync(id, cancellationToken);
        return camera is null
            ? Results.Problem($"Камера {id} не найдена.", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(camera);
    }

    /// <summary>Возвращает сетевые параметры сохранённой камеры (сейчас — состояние DHCP).</summary>
    /// <remarks>
    /// Значение <c>dhcpEnabled = null</c> означает, что камера не сообщила параметр.
    /// Ответы: 200 — параметры (в том числе с неизвестным значением); 404 — камера не найдена.
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

    /// <summary>Проверяет подключение и добавляет камеру в реестр.</summary>
    /// <remarks>
    /// Тело — <see cref="CameraRegistrationRequest"/>. Перед сохранением выполняется полный опрос камеры,
    /// профили main/sub сохраняются в БД, потоки регистрируются в медиа-ядре.
    /// Ответы: 201 — камера добавлена; 400 — некорректные параметры; 401 — неверные учётные данные;
    /// 422 — устройство не поддерживает операцию; 502 — камера недоступна.
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

    /// <summary>Повторно опрашивает камеру и обновляет её параметры и профили.</summary>
    /// <remarks>
    /// Тело — <see cref="CameraRegistrationRequest"/>. Пустой пароль означает «оставить прежний».
    /// Ответы: 200 — камера обновлена; 400 — некорректные параметры; 401 — неверные учётные данные;
    /// 404 — камера не найдена; 422 — устройство не поддерживает операцию; 502 — камера недоступна.
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

    /// <summary>Удаляет камеру из реестра и её потоки из медиа-ядра.</summary>
    /// <remarks>Ответы: 204 — камера удалена; 404 — камера не найдена.</remarks>
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

    /// <summary>Включает или выключает камеру (неактивные не показываются в live-сетке).</summary>
    /// <remarks>
    /// Тело — <see cref="CameraActiveRequest"/>. При выключении потоки камеры снимаются с медиа-ядра,
    /// при включении — регистрируются заново.
    /// Ответы: 200 — камера обновлена; 404 — камера не найдена.
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

    /// <summary>JPEG-снапшот профиля сохранённой камеры.</summary>
    /// <remarks>
    /// Профиль задаётся строкой запроса <c>profile</c> (<c>main</c> или <c>sub</c>).
    /// Ответы: 200 — <c>image/jpeg</c>; 400 — некорректный профиль; 404 — камера не найдена;
    /// 401/422/502 — ошибки подключения к камере.
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

    /// <summary>WHEP-прокси для сохранённой камеры: субпоток для тайла, основной — для fullscreen.</summary>
    /// <remarks>
    /// Профиль задаётся строкой запроса <c>profile</c> (<c>sub</c> по умолчанию, <c>main</c> для fullscreen).
    /// Конкретный субпоток можно выбрать строкой запроса <c>profileId</c>; без неё используется
    /// субпоток, сохранённый в настройках камеры. Тело запроса — SDP offer, ответ — SDP answer go2rtc.
    /// Поток предварительно регистрируется в медиа-ядре с учётными данными камеры.
    /// Ответы: 200 — сессия установлена; 400 — пустой offer или неизвестный профиль;
    /// 404 — камера не найдена; 422 — у камеры нет запрошенного профиля; 502 — ошибка медиа-ядра.
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
            return Results.BadRequest(new { error = "Тело запроса должно содержать SDP offer." });
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
        var other => throw new ArgumentException($"Неизвестный профиль «{other}»: ожидается main или sub.", nameof(profile)),
    };

    private static IResult ToProblem(CameraModuleException exception) => exception switch
    {
        CameraAuthenticationException => Results.Problem(exception.Message, statusCode: StatusCodes.Status401Unauthorized),
        CameraOperationNotSupportedException => Results.Problem(exception.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        _ => Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway),
    };
}

/// <summary>Параметры подключения к камере для эндпоинтов <c>/api/cameras</c>.</summary>
/// <param name="Host">IP-адрес или имя камеры.</param>
/// <param name="RtspPort">Порт RTSP (по умолчанию 554).</param>
/// <param name="UserName">Логин учётной записи камеры.</param>
/// <param name="Password">Пароль учётной записи камеры (наружу не возвращается).</param>
/// <param name="Vendor">Подсказка модуля производителя (<c>hikvision</c>, <c>onvif</c>); пусто — автоподбор.</param>
/// <param name="Profile">Профиль потока для снапшота: <c>main</c> или <c>sub</c>.</param>
/// <param name="ProfileId">Идентификатор конкретного профиля для снапшота; имеет приоритет над <c>profile</c>.</param>
public sealed record CameraRequest(
    string? Host,
    int? RtspPort,
    string? UserName,
    string? Password,
    string? Vendor,
    string? Profile,
    string? ProfileId = null)
{
    /// <summary>Проверяет параметры и создаёт цель опроса.</summary>
    public CameraTarget ToTarget()
    {
        var address = ParseHost(Host);

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new ArgumentException("Укажите логин камеры.", nameof(UserName));
        }

        if (string.IsNullOrEmpty(Password))
        {
            throw new ArgumentException("Укажите пароль камеры.", nameof(Password));
        }

        var endpoint = new CameraEndpoint(address, RtspPort ?? 554);

        var credentials = new CameraCredentials(UserName, Password);
        var vendor = string.IsNullOrWhiteSpace(Vendor) ? null : Vendor.Trim().ToLowerInvariant();

        return new CameraTarget(endpoint, credentials, vendor);
    }

    /// <summary>Профиль потока из поля <c>profile</c> (по умолчанию основной).</summary>
    public CameraStreamProfileKind ProfileKind() => Profile?.Trim().ToLowerInvariant() switch
    {
        null or "" or "main" => CameraStreamProfileKind.Main,
        "sub" => CameraStreamProfileKind.Sub,
        var other => throw new ArgumentException($"Неизвестный профиль «{other}»: ожидается main или sub.", nameof(Profile)),
    };

    private static IPAddress ParseHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Укажите IP-адрес или имя камеры.", nameof(Host));
        }

        if (IPAddress.TryParse(host.Trim(), out var address))
        {
            return address;
        }

        throw new ArgumentException($"«{host}» не является IP-адресом камеры.", nameof(Host));
    }
}

/// <summary>Параметры добавления или изменения сохранённой камеры.</summary>
/// <param name="Name">Отображаемое имя; пусто — имя устройства или «производитель модель».</param>
/// <param name="Host">IP-адрес камеры.</param>
/// <param name="RtspPort">Порт RTSP (по умолчанию 554).</param>
/// <param name="UserName">Логин учётной записи камеры.</param>
/// <param name="Password">Пароль; при изменении пустой пароль означает «оставить прежний».</param>
/// <param name="Vendor">Подсказка модуля производителя (<c>hikvision</c>, <c>onvif</c>); пусто — автоподбор.</param>
/// <param name="SubProfileId">Идентификатор выбранного субпотока; пусто — первый субпоток.</param>
/// <param name="MainProfileId">Идентификатор основного профиля; пусто — профиль, выбранный модулем.</param>
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
    /// <summary>Переносит параметры запроса в сценарий реестра камер.</summary>
    public CameraRegistration ToRegistration() =>
        new(Name, Host, RtspPort, UserName, Password, Vendor, SubProfileId, MainProfileId);
}

/// <summary>Настройки сохранённой камеры, не требующие повторного опроса устройства.</summary>
/// <param name="Name">Новое отображаемое имя; пусто — оставить прежнее.</param>
/// <param name="SubProfileId">Идентификатор субпотока; пусто — оставить прежний.</param>
public sealed record CameraSettingsRequest(string? Name, string? SubProfileId);

/// <summary>Признак активности сохранённой камеры.</summary>
/// <param name="Active"><c>true</c> — показывать в live-сетке, <c>false</c> — скрыть.</param>
public sealed record CameraActiveRequest(bool Active);
