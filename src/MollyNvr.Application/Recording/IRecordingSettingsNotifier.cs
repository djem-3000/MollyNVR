namespace MollyNvr.Application.Recording;

/// <summary>
/// Signal that recording settings changed: the recording loop wakes up immediately instead of
/// waiting for the next schedule poll.
/// </summary>
public interface IRecordingSettingsNotifier
{
        /// <summary>Tells the recording loop that the settings may have changed.</summary>
    void Notify();

        /// <summary>Waits for the signal no longer than <paramref name="timeout"/>.</summary>
    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
