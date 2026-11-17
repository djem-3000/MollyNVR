using MollyNvr.Application.Recording;

namespace MollyNvr.Infrastructure.Recording;

/// <summary>
/// Реализация сигнала изменения настроек записи. Один ожидающий на цикл записи:
/// если сигнал пришёл во время обработки, следующий <see cref="WaitAsync"/> не ждёт.
/// </summary>
public sealed class RecordingSettingsNotifier : IRecordingSettingsNotifier
{
    private readonly object _gate = new();

    private TaskCompletionSource? _waiter;
    private bool _pending;

    public void Notify()
    {
        TaskCompletionSource? waiter;

        lock (_gate)
        {
            waiter = _waiter;
            _waiter = null;

            if (waiter is null)
            {
                _pending = true;
                return;
            }
        }

        waiter.TrySetResult();
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        TaskCompletionSource waiter;

        lock (_gate)
        {
            if (_pending)
            {
                _pending = false;
                return;
            }

            waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiter = waiter;
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await waiter.Task.WaitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // интервал опроса истёк — это штатное завершение ожидания
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_waiter, waiter))
                {
                    _waiter = null;
                }
            }
        }
    }
}
