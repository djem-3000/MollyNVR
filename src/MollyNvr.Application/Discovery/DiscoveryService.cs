using System.Collections.Concurrent;
using System.Net;
using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Discovery;

/// <summary>Поиск камер: multicast-протоколы, сканирование подсети и проверка одного адреса.</summary>
public sealed class DiscoveryService(
    IPortScanner portScanner,
    IEnumerable<IDiscoveryProtocol> protocols,
    IArpTableResolver arpTableResolver,
    IOuiLookup ouiLookup,
    IEnumerable<ICameraModule> modules,
    IAliveHostScanner aliveHostScanner)
{
    private const int HostScanConcurrency = 16;
    private const int MulticastScanConcurrency = 4;
    private const int MaxNeighbourScan = 256;

    private static readonly int[] CameraPorts = [554, 8554, 8000, 9000, 34567, 37777];

    private readonly IReadOnlyList<IDiscoveryProtocol> _protocols = protocols.ToList();
    private readonly IReadOnlyList<ICameraModule> _modules = modules.ToList();

    /// <summary>
    /// Сканирует подсеть или диапазон: сначала быстрый набор портов камер,
    /// затем полный список портов у найденных хостов. Сохранённые камеры тоже показываются.
    /// </summary>
    public async Task<IReadOnlyList<DiscoveredCameraView>> ScanUnicastAsync(
        string? range,
        CancellationToken cancellationToken)
    {
        var hosts = IpRange.Parse(range).Hosts();
        var macAddresses = await GetMacAddressesAsync(cancellationToken);

        var alive = await FindAliveHostsAsync(hosts, cancellationToken);
        var targets = alive is { Count: > 0 }
            ? hosts.Where(alive.Contains).ToList()
            : hosts;

        var found = await ScanHostsAsync(targets, macAddresses, cancellationToken);

        if (alive is { Count: > 0 } && found.Count == 0)
        {
            // ICMP ответили не все устройства — перебираем весь диапазон, как раньше.
            found = await ScanHostsAsync(hosts, macAddresses, cancellationToken);
        }

        return await ToViewsAsync(found, cancellationToken);
    }

    /// <summary>
    /// Сканирует порты указанных хостов. Полный перебор /24 портами дорог и теряется
    /// в NAT Docker Desktop, поэтому список хостов заранее сужается ICMP-опросом.
    /// </summary>
    private async Task<IReadOnlyList<DiscoveredCandidate>> ScanHostsAsync(
        IReadOnlyList<IPAddress> hosts,
        IReadOnlyDictionary<IPAddress, string> macAddresses,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentBag<DiscoveredCandidate>();

        await Parallel.ForEachAsync(
            hosts,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = HostScanConcurrency,
                CancellationToken = cancellationToken,
            },
            async (host, token) =>
            {
                var discoveryPorts = await portScanner.FindOpenPortsAsync(
                    host,
                    CameraPortCatalog.DiscoveryPorts,
                    token);

                if (!LooksLikeCamera(discoveryPorts))
                {
                    return;
                }

                var ports = await portScanner.FindOpenPortsAsync(
                    host,
                    CameraPortCatalog.ScanPorts.Except(discoveryPorts).ToList(),
                    token);

                found.Add(new DiscoveredCandidate(
                    host,
                    discoveryPorts.Union(ports).Order().ToList(),
                    macAddresses.GetValueOrDefault(host)));
            });

        return found.ToList();
    }

    private async Task<IReadOnlySet<IPAddress>?> FindAliveHostsAsync(
        IReadOnlyList<IPAddress> hosts,
        CancellationToken cancellationToken)
    {
        try
        {
            var alive = await aliveHostScanner.FindAliveHostsAsync(hosts, cancellationToken);
            if (alive is not { Count: > 0 })
            {
                return alive;
            }

            // Известные соседи из ARP тоже заслуживают проверки: ICMP на них мог не пройти.
            var withNeighbours = new HashSet<IPAddress>(alive);
            withNeighbours.UnionWith((await GetMacAddressesAsync(cancellationToken)).Keys);
            return withNeighbours;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Ищет камеры multicast-протоколами и сканирует порты найденных устройств.</summary>
    public async Task<IReadOnlyList<DiscoveredCameraView>> ScanMulticastAsync(
        CancellationToken cancellationToken)
    {
        var macAddresses = await GetMacAddressesAsync(cancellationToken);

        var responses = await Task.WhenAll(
            _protocols.Select(protocol => DiscoverSafelyAsync(protocol, cancellationToken)));

        var merged = MergeResponses(responses.SelectMany(static response => response));
        var candidates = new ConcurrentBag<DiscoveredCandidate>();

        await Parallel.ForEachAsync(
            BuildMulticastTargets(merged, macAddresses),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MulticastScanConcurrency,
                CancellationToken = cancellationToken,
            },
            async (target, token) =>
            {
                var ports = await portScanner.FindOpenPortsAsync(
                    target.Address,
                    CameraPortCatalog.ScanPorts,
                    token);

                if (!LooksLikeCamera(ports))
                {
                    return;
                }

                candidates.Add(new DiscoveredCandidate(
                    target.Address,
                    ports,
                    target.Response?.Mac ?? macAddresses.GetValueOrDefault(target.Address),
                    target.Response?.VendorKey,
                    target.Response?.Vendor,
                    target.Response?.Model));
            });

        return await ToViewsAsync(candidates, cancellationToken);
    }

    /// <summary>Проверяет один адрес и возвращает найденное устройство (или пустой список).</summary>
    public async Task<IReadOnlyList<DiscoveredCameraView>> ScanAddressAsync(
        string? host,
        string? vendorKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            !IPAddress.TryParse(host.Trim(), out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Укажите корректный IPv4-адрес.", nameof(host));
        }

        var (moduleKey, vendor) = ResolveVendor(vendorKey);
        var ports = await portScanner.FindOpenPortsAsync(
            address,
            CameraPortCatalog.ScanPorts,
            cancellationToken);
        var macAddresses = await GetMacAddressesAsync(cancellationToken);

        var candidate = new DiscoveredCandidate(
            address,
            ports,
            macAddresses.GetValueOrDefault(address),
            moduleKey,
            vendor);

        return await ToViewsAsync([candidate], cancellationToken);
    }

    private async Task<IReadOnlyList<DiscoveredCameraView>> ToViewsAsync(
        IEnumerable<DiscoveredCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var ordered = candidates
            .OrderBy(static candidate => IpRange.ToUInt32(candidate.Address))
            .ToList();

        using var semaphore = new SemaphoreSlim(4);

        return await Task.WhenAll(ordered.Select(async candidate =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                return await ToViewAsync(candidate, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }));
    }

    private async Task<DiscoveredCameraView> ToViewAsync(
        DiscoveredCandidate candidate,
        CancellationToken cancellationToken)
    {
        var vendorKey = candidate.VendorKey;
        var vendor = NormalizeVendor(candidate.Vendor);

        if (string.IsNullOrWhiteSpace(vendor) && !string.IsNullOrWhiteSpace(candidate.Mac))
        {
            vendor = ouiLookup.FindVendor(candidate.Mac);
        }

        if (string.IsNullOrWhiteSpace(vendorKey) && !string.IsNullOrWhiteSpace(vendor))
        {
            vendorKey = FindModuleKey(vendor);
        }

        if (string.IsNullOrWhiteSpace(vendor))
        {
            var (identifiedKey, identifiedVendor) = await IdentifyAsync(candidate.Address, cancellationToken);
            vendorKey ??= identifiedKey;
            vendor ??= identifiedVendor;
        }

        return new DiscoveredCameraView(
            Host: candidate.Address.ToString(),
            Mac: candidate.Mac,
            VendorKey: vendorKey,
            Vendor: vendor,
            Model: candidate.Model,
            OpenPorts: candidate.OpenPorts);
    }

    /// <summary>«ONVIF» — название протокола, а не производитель: такое значение уточняется по MAC/OUI.</summary>
    private static string? NormalizeVendor(string? vendor) =>
        string.IsNullOrWhiteSpace(vendor) ||
        string.Equals(vendor, "ONVIF", StringComparison.OrdinalIgnoreCase)
            ? null
            : vendor;

    private async Task<(string? VendorKey, string? Vendor)> IdentifyAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        var endpoint = new CameraEndpoint(address);

        foreach (var module in _modules)
        {
            try
            {
                if (await module.CanHandleAsync(endpoint, cancellationToken))
                {
                    return (module.Key, module.DisplayName);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Устройство не ответило по протоколу модуля — пробуем следующий.
            }
        }

        return (null, null);
    }

    private static async Task<IReadOnlyList<DiscoveryResponse>> DiscoverSafelyAsync(
        IDiscoveryProtocol protocol,
        CancellationToken cancellationToken)
    {
        try
        {
            return await protocol.DiscoverAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Протокол недоступен в текущей сети (например, multicast в bridge-контейнере).
            return [];
        }
    }

    private static IReadOnlyList<MulticastTarget> BuildMulticastTargets(
        IReadOnlyList<DiscoveryResponse> responses,
        IReadOnlyDictionary<IPAddress, string> macAddresses)
    {
        var known = responses
            .Select(static response => response.Address)
            .ToHashSet();

        var neighbours = macAddresses.Keys
            .Where(address => !known.Contains(address))
            .OrderBy(IpRange.ToUInt32)
            .Take(MaxNeighbourScan)
            .Select(static address => new MulticastTarget(address, Response: null));

        var targets = responses
            .Select(static response => new MulticastTarget(response.Address, response))
            .Concat(neighbours)
            .ToList();

        return targets;
    }

    private static IReadOnlyList<DiscoveryResponse> MergeResponses(
        IEnumerable<DiscoveryResponse> responses)
    {
        return responses
            .GroupBy(static response => response.Address)
            .Select(static group =>
            {
                var preferred = group.FirstOrDefault(static response =>
                        !string.IsNullOrWhiteSpace(response.Vendor) &&
                        !string.Equals(response.VendorKey, "onvif", StringComparison.OrdinalIgnoreCase))
                    ?? group.FirstOrDefault(static response => !string.IsNullOrWhiteSpace(response.Vendor));

                return new DiscoveryResponse(
                    Address: group.Key,
                    Mac: group.Select(static response => response.Mac).FirstOrDefault(static mac => !string.IsNullOrWhiteSpace(mac)),
                    VendorKey: preferred?.VendorKey ?? group.Select(static response => response.VendorKey).FirstOrDefault(static key => !string.IsNullOrWhiteSpace(key)),
                    Vendor: preferred?.Vendor,
                    Model: group.Select(static response => response.Model).FirstOrDefault(static model => !string.IsNullOrWhiteSpace(model)));
            })
            .ToList();
    }

    private async Task<IReadOnlyDictionary<IPAddress, string>> GetMacAddressesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await arpTableResolver.GetMacAddressesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new Dictionary<IPAddress, string>();
        }
    }

    private (string? Key, string? Vendor) ResolveVendor(string? vendorKey)
    {
        if (string.IsNullOrWhiteSpace(vendorKey))
        {
            return (null, null);
        }

        var module = _modules.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, vendorKey.Trim(), StringComparison.OrdinalIgnoreCase));

        return module is null
            ? throw new ArgumentException($"Модуль производителя «{vendorKey}» не найден.", nameof(vendorKey))
            : (module.Key, module.DisplayName);
    }

    private string? FindModuleKey(string vendor) =>
        _modules.FirstOrDefault(module =>
            string.Equals(module.DisplayName, vendor, StringComparison.OrdinalIgnoreCase))?.Key;

    private static bool LooksLikeCamera(IReadOnlyList<int> ports) =>
        ports.Any(static port => CameraPorts.Contains(port));

    private sealed record DiscoveredCandidate(
        IPAddress Address,
        IReadOnlyList<int> OpenPorts,
        string? Mac,
        string? VendorKey = null,
        string? Vendor = null,
        string? Model = null);

    private sealed record MulticastTarget(IPAddress Address, DiscoveryResponse? Response);
}
