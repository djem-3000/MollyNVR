namespace MollyNvr.Application.Cameras;

/// <summary>No camera with the specified identifier.</summary>
public sealed class CameraNotFoundException(Guid id)
        : Exception($"Camera {id} not found.");
