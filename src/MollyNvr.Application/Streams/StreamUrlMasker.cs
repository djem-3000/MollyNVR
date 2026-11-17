namespace MollyNvr.Application.Streams;

/// <summary>Маскирование кредов в URL перед логированием и отдачей наружу.</summary>
public static class StreamUrlMasker
{
    public static string Mask(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return url;
        }

        var authorityStart = schemeEnd + 3;
        var authorityEnd = url.IndexOfAny(['/', '?', '#'], authorityStart);
        if (authorityEnd < 0)
        {
            authorityEnd = url.Length;
        }

        var at = url.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
        if (at < authorityStart)
        {
            return url;
        }

        var colon = url.IndexOf(':', authorityStart, at - authorityStart);
        if (colon < 0)
        {
            return url;
        }

        return string.Concat(url.AsSpan(0, colon + 1), "***", url.AsSpan(at));
    }
}
