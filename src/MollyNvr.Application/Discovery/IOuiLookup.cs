namespace MollyNvr.Application.Discovery;

/// <summary>Vendor lookup by the first three bytes of the MAC address (OUI).</summary>
public interface IOuiLookup
{
        /// <summary>Vendor name, or <c>null</c> when the prefix is unknown.</summary>
    string? FindVendor(string? macAddress);
}
