namespace MollyNvr.Application.Discovery;

/// <summary>Определение производителя по первым трём байтам MAC-адреса (OUI).</summary>
public interface IOuiLookup
{
    /// <summary>Название производителя или <c>null</c>, если префикс неизвестен.</summary>
    string? FindVendor(string? macAddress);
}
