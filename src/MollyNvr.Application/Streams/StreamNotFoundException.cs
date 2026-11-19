namespace MollyNvr.Application.Streams;

public sealed class StreamNotFoundException(string streamId)
        : Exception($"Stream '{streamId}' was not found.");
