namespace MollyNvr.Cameras.Abstractions;

/// <summary>Building an RTSP URL with credentials (the password is encoded for userinfo).</summary>
public static class RtspUriFactory
{
        /// <summary>Inserts the login and password into the device RTSP URL.</summary>
    public static string WithCredentials(Uri rtspUri, CameraCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(rtspUri);
        ArgumentNullException.ThrowIfNull(credentials);

        var builder = new UriBuilder(rtspUri)
        {
            UserName = Uri.EscapeDataString(credentials.UserName),
            Password = Uri.EscapeDataString(credentials.Password),
        };

        return builder.Uri.AbsoluteUri;
    }
}
