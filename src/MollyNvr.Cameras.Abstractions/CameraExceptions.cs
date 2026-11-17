namespace MollyNvr.Cameras.Abstractions;

/// <summary>Базовая ошибка модуля камеры.</summary>
public class CameraModuleException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Камера отклонила учётные данные (HTTP/RSTP 401/403).</summary>
public sealed class CameraAuthenticationException(string message, Exception? innerException = null)
    : CameraModuleException(message, innerException);

/// <summary>Камера недоступна: сеть, таймаут, некорректный ответ.</summary>
public sealed class CameraUnavailableException(string message, Exception? innerException = null)
    : CameraModuleException(message, innerException);

/// <summary>Устройство ответило, но не поддерживает запрошенную операцию.</summary>
public sealed class CameraOperationNotSupportedException(string message)
    : CameraModuleException(message);
