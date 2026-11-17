using MollyNvr.Infrastructure.Recording;

namespace MollyNvr.Infrastructure.Tests;

public sealed class RecordingSettingsNotifierTests
{
    [Fact]
    public async Task WaitAsync_ReturnsImmediately_AfterNotify()
    {
        var notifier = new RecordingSettingsNotifier();

        notifier.Notify();
        var started = DateTimeOffset.UtcNow;

        await notifier.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WaitAsync_ReturnsImmediately_WhenNotifiedWhileWaiting()
    {
        var notifier = new RecordingSettingsNotifier();

        var waiting = notifier.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None);
        notifier.Notify();

        await waiting.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task WaitAsync_ReturnsAfterTimeout_WithoutNotify()
    {
        var notifier = new RecordingSettingsNotifier();
        var started = DateTimeOffset.UtcNow;

        await notifier.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(90));
    }

    [Fact]
    public async Task WaitAsync_Throws_WhenCancelled()
    {
        var notifier = new RecordingSettingsNotifier();
        using var cancellation = new CancellationTokenSource();

        var waiting = notifier.WaitAsync(TimeSpan.FromSeconds(30), cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
}
