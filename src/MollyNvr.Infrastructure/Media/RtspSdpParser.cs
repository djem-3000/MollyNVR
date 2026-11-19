using System.Text;
using MollyNvr.Application.Cameras;
using MollyNvr.Cameras.Abstractions;
using MollyNvr.Infrastructure.Media;

namespace MollyNvr.Infrastructure.Media;

/// <summary>SDP parsing (RFC 4566): video and audio stream codecs.</summary>
public static class RtspSdpParser
{
        /// <summary>Extracts codecs from the session SDP description.</summary>
    public static RtspMediaInfo? Parse(string? sdp)
    {
        if (string.IsNullOrWhiteSpace(sdp))
        {
            return null;
        }

        string? videoCodec = null;
        string? audioCodec = null;
        int? audioSampleRate = null;

        string? media = null;
        var payloads = new Dictionary<string, string>(StringComparer.Ordinal);
        var audioPayloadIds = new HashSet<string>(StringComparer.Ordinal);
        var videoPayloadIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in sdp.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 2 || line[1] != '=')
            {
                continue;
            }

            var value = line[2..];
            switch (line[0])
            {
                case 'm':
                    var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    media = parts.Length > 0 ? parts[0].ToLowerInvariant() : null;
                    if (media is "audio" or "video" && parts.Length > 3)
                    {
                        var target = media == "audio" ? audioPayloadIds : videoPayloadIds;
                        foreach (var payload in parts.Skip(3))
                        {
                            target.Add(payload);
                        }
                    }

                    break;

                case 'a' when value.StartsWith("rtpmap:", StringComparison.Ordinal):
                    var mapping = value["rtpmap:".Length..];
                    var separator = mapping.IndexOf(' ');
                    if (separator <= 0)
                    {
                        break;
                    }

                    payloads[mapping[..separator]] = mapping[(separator + 1)..].Trim();
                    break;
            }
        }

        foreach (var (payloadId, encoding) in payloads)
        {
            var tokens = encoding.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var name = tokens[0];

            if (videoCodec is null && videoPayloadIds.Contains(payloadId))
            {
                videoCodec = VideoCodecNames.Normalize(name);
                continue;
            }

            if (audioCodec is null && audioPayloadIds.Contains(payloadId))
            {
                audioCodec = AudioCodecNames.Normalize(name);
                if (tokens.Length > 1 &&
                    int.TryParse(tokens[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var clockRate))
                {
                    audioSampleRate = clockRate;
                }
            }
        }

        if (videoCodec is null && audioCodec is null)
        {
            return null;
        }

        return new RtspMediaInfo(videoCodec, audioCodec, audioSampleRate);
    }

        /// <summary>Extracts codecs from the SDP <c>Content-Type</c> header when it is passed as a string.</summary>
    public static RtspMediaInfo? Parse(ReadOnlySpan<byte> sdp) => Parse(Encoding.UTF8.GetString(sdp));
}
