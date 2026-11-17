using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>ARP-таблица хоста: IP → MAC. В контейнере может быть неполной.</summary>
public interface IArpTableResolver
{
    /// <summary>Возвращает известные MAC-адреса; при недоступности таблицы — пустой словарь.</summary>
    Task<IReadOnlyDictionary<IPAddress, string>> GetMacAddressesAsync(
        CancellationToken cancellationToken);
}
