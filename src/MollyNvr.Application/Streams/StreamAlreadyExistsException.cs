namespace MollyNvr.Application.Streams;

public sealed class StreamAlreadyExistsException(string streamId)
    : Exception($"Поток «{streamId}» уже существует.");
