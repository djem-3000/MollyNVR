using System.Net;

namespace MollyNvr.Application.Discovery;

/// <summary>
/// Быстрая проверка, какие адреса диапазона вообще отвечают (ICMP echo).
/// Нужна, чтобы сканировать порты только у живых хостов: полный перебор портов
/// по /24 создаёт лавину соединений, которую теряет NAT Docker Desktop.
/// </summary>
public interface IAliveHostScanner
{
    /// <summary>
    /// Возвращает отвечающие адреса; <c>null</c> — проверка недоступна на этой платформе
    /// или без прав, тогда вызывающий код сканирует весь диапазон.
    /// </summary>
    Task<IReadOnlySet<IPAddress>?> FindAliveHostsAsync(
        IReadOnlyList<IPAddress> hosts,
        CancellationToken cancellationToken);
}
