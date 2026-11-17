namespace MollyNvr.Cameras.Abstractions;

/// <summary>Учётные данные камеры. Пароль не попадает в строковое представление и логи.</summary>
public sealed record CameraCredentials(string UserName, string Password)
{
    public override string ToString() => $"{UserName}:***";
}
