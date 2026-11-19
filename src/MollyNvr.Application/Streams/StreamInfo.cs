namespace MollyNvr.Application.Streams;

/// <summary>Stream served by the API (URL with masked credentials).</summary>
public sealed record StreamInfo(string Id, string Url);
