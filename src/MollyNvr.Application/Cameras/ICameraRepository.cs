using MollyNvr.Cameras.Abstractions;
using MollyNvr.Domain.Cameras;

namespace MollyNvr.Application.Cameras;

/// <summary>Хранилище камер. Учётные данные сохраняются отдельно и шифруются.</summary>
public interface ICameraRepository
{
    /// <summary>Все камеры, отсортированные по имени.</summary>
    Task<IReadOnlyList<Camera>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Камера по идентификатору или <c>null</c>.</summary>
    Task<Camera?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Сохраняет новую камеру вместе с учётными данными.</summary>
    Task AddAsync(Camera camera, CameraCredentials credentials, CancellationToken cancellationToken);

    /// <summary>
    /// Обновляет камеру. <paramref name="credentials"/> = <c>null</c> — сохранить прежние учётные данные.
    /// </summary>
    Task UpdateAsync(Camera camera, CameraCredentials? credentials, CancellationToken cancellationToken);

    /// <summary>Удаляет камеру и её учётные данные.</summary>
    Task RemoveAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Расшифрованные учётные данные камеры или <c>null</c>.</summary>
    Task<CameraCredentials?> GetCredentialsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Меняет признак активности; возвращает обновлённую камеру или <c>null</c>, если её нет.</summary>
    Task<Camera?> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
