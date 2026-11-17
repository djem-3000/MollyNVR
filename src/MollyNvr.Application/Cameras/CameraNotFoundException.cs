namespace MollyNvr.Application.Cameras;

/// <summary>Камера с указанным идентификатором не найдена.</summary>
public sealed class CameraNotFoundException(Guid id)
    : Exception($"Камера {id} не найдена.");
