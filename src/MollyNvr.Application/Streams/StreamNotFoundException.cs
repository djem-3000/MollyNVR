namespace MollyNvr.Application.Streams;

public sealed class StreamNotFoundException(string streamId)
    : Exception($"Поток «{streamId}» не найден.");
