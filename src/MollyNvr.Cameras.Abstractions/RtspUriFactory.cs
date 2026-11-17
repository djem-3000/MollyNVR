namespace MollyNvr.Cameras.Abstractions;

/// <summary>Сборка RTSP-URL с учётными данными (пароль кодируется для userinfo).</summary>
public static class RtspUriFactory
{
    /// <summary>Вставляет логин и пароль в RTSP-URL устройства.</summary>
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
