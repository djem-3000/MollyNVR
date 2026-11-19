namespace MollyNvr.Cameras.Abstractions;

/// <summary>Base error of a camera module.</summary>
public class CameraModuleException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>The camera rejected the credentials (HTTP/RTSP 401/403).</summary>
public sealed class CameraAuthenticationException(string message, Exception? innerException = null)
    : CameraModuleException(message, innerException);

/// <summary>The camera is unavailable: network, timeout, invalid response.</summary>
public sealed class CameraUnavailableException(string message, Exception? innerException = null)
    : CameraModuleException(message, innerException);

/// <summary>The device responded but does not support the requested operation.</summary>
public sealed class CameraOperationNotSupportedException(string message)
    : CameraModuleException(message);
