namespace MollyNvr.Application.Streams;

public sealed class StreamAlreadyExistsException(string streamId)
        : Exception($"Stream '{streamId}' already exists.");
