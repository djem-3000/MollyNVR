namespace MollyNvr.Domain.Streams;

/// <summary>Stream identifier in the media engine: Latin letters, digits, hyphen, underscore.</summary>
public sealed record StreamId
{
    public const int MaxLength = 64;

    private StreamId(string value) => Value = value;

    public string Value { get; }

    public static StreamId Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
                        throw new ArgumentException("The stream identifier cannot be empty.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength || !trimmed.All(static symbol => char.IsAsciiLetterOrDigit(symbol) || symbol is '-' or '_'))
        {
            throw new ArgumentException(
                                $"The stream identifier may contain only Latin letters, digits, hyphen and underscore (at most {MaxLength} characters).",
                nameof(value));
        }

        return new StreamId(trimmed);
    }

    public override string ToString() => Value;
}
