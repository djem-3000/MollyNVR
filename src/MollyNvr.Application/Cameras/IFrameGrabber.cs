namespace MollyNvr.Application.Cameras;

/// <summary>
/// Getting a JPEG frame from an RTSP source for cameras without a native snapshot
/// (for example, when the camera HTTP port is closed).
/// </summary>
public interface IFrameGrabber
{
        /// <summary>Returns a JPEG frame of the source. The source contains credentials and is never logged.</summary>
    Task<byte[]> GrabJpegAsync(string sourceUrl, CancellationToken cancellationToken);
}
