namespace MollyNvr.Cameras.Abstractions;

/// <summary>Camera credentials. The password never appears in the string representation or logs.</summary>
public sealed record CameraCredentials(string UserName, string Password)
{
    public override string ToString() => $"{UserName}:***";
}
