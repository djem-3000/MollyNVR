namespace MollyNvr.Domain.Streams;

/// <summary>Идентификатор потока в медиа-ядре: латинские буквы, цифры, дефис, подчёркивание.</summary>
public sealed record StreamId
{
    public const int MaxLength = 64;

    private StreamId(string value) => Value = value;

    public string Value { get; }

    public static StreamId Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Идентификатор потока не может быть пустым.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength || !trimmed.All(static symbol => char.IsAsciiLetterOrDigit(symbol) || symbol is '-' or '_'))
        {
            throw new ArgumentException(
                $"Идентификатор потока может содержать только латинские буквы, цифры, дефис и подчёркивание (не более {MaxLength} символов).",
                nameof(value));
        }

        return new StreamId(trimmed);
    }

    public override string ToString() => Value;
}
