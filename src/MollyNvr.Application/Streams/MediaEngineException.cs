namespace MollyNvr.Application.Streams;

/// <summary>Медиа-ядро недоступно или вернуло ошибку.</summary>
public sealed class MediaEngineException : Exception
{
    public MediaEngineException(string message)
        : base(message)
    {
    }

    public MediaEngineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
