namespace MollyNvr.Application.Streams;

/// <summary>Поток для отдачи в API (URL — с маскированными кредами).</summary>
public sealed record StreamInfo(string Id, string Url);
