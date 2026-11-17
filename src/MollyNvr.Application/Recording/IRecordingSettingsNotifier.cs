namespace MollyNvr.Application.Recording;

/// <summary>
/// Сигнал об изменении настроек записи: цикл записи просыпается сразу, не дожидаясь
/// очередного опроса расписания.
/// </summary>
public interface IRecordingSettingsNotifier
{
    /// <summary>Сообщает циклу записи, что настройки могли измениться.</summary>
    void Notify();

    /// <summary>Ждёт сигнал не дольше <paramref name="timeout"/>.</summary>
    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
