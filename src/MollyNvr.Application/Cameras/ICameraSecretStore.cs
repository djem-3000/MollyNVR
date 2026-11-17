using MollyNvr.Cameras.Abstractions;

namespace MollyNvr.Application.Cameras;

/// <summary>Шифрование учётных данных камер перед сохранением в БД.</summary>
public interface ICameraSecretStore
{
    /// <summary>Шифрует учётные данные в строку для хранения.</summary>
    string Protect(CameraCredentials credentials);

    /// <summary>Расшифровывает учётные данные; при повреждении данных — исключение.</summary>
    CameraCredentials Unprotect(string payload);
}
