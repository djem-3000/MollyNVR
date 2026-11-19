namespace MollyNvr.Application.Streams;

/// <summary>The media engine is unavailable or returned an error.</summary>
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
