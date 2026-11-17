using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using MollyNvr.Application.Discovery;

namespace MollyNvr.Infrastructure.Discovery;

/// <summary>Чтение ARP-таблицы хоста. В контейнере обычно содержит только шлюз.</summary>
public sealed partial class ArpTableResolver : IArpTableResolver
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyDictionary<IPAddress, string>> GetMacAddressesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                return await ReadProcArpAsync(cancellationToken);
            }

            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                return await ReadCommandArpAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Таблица ARP недоступна — поиск продолжится без MAC-адресов.
        }

        return new Dictionary<IPAddress, string>();
    }

    private static async Task<IReadOnlyDictionary<IPAddress, string>> ReadProcArpAsync(
        CancellationToken cancellationToken)
    {
        const string path = "/proc/net/arp";
        if (!File.Exists(path))
        {
            return new Dictionary<IPAddress, string>();
        }

        var result = new Dictionary<IPAddress, string>();
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 ||
                parts[2] == "0x0" ||
                !IPAddress.TryParse(parts[0], out var address) ||
                !IsMacAddress(parts[3]))
            {
                continue;
            }

            result[address] = parts[3];
        }

        return result;
    }

    private static async Task<IReadOnlyDictionary<IPAddress, string>> ReadCommandArpAsync(
        CancellationToken cancellationToken)
    {
        var output = await RunArpAsync(cancellationToken);
        var result = new Dictionary<IPAddress, string>();

        foreach (Match match in ArpEntryRegex().Matches(output))
        {
            if (IPAddress.TryParse(match.Groups["ip"].Value, out var address))
            {
                result[address] = match.Groups["mac"].Value.Replace('-', ':');
            }
        }

        return result;
    }

    private static async Task<string> RunArpAsync(CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "arp",
                Arguments = OperatingSystem.IsWindows() ? "-a" : "-an",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(CommandTimeout);

        var output = await process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        await process.WaitForExitAsync(timeoutSource.Token);
        return output;
    }

    private static bool IsMacAddress(string value) =>
        value.Length >= 17 && value.Split(':').Length == 6;

    [GeneratedRegex(
        @"(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>(?:[0-9a-fA-F]{2}[:-]){5}[0-9a-fA-F]{2})",
        RegexOptions.CultureInvariant)]
    private static partial Regex ArpEntryRegex();
}
