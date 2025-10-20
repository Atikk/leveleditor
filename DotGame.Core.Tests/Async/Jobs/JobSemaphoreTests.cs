using System;
using System.Threading;
using System.Threading.Tasks;
using DotGame.Core.Async.Jobs;
using Xunit;

namespace DotGame.Core.Tests.Async.Jobs;

public sealed class JobSemaphoreTests
{
    [Fact]
    public async Task WaitAsync_BlocksUntilRelease()
    {
        var semaphore = new JobSemaphore(initialCount: 1);

        Assert.True(semaphore.TryWait());

        var waitTask = semaphore.WaitAsync();
        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);

        semaphore.Release();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WaitAsync_CancellationDoesNotLeakPermits()
    {
        var semaphore = new JobSemaphore(initialCount: 0, maxCount: 1);
        using var cts = new CancellationTokenSource();

        var waitTask = semaphore.WaitAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(async () => await waitTask);

        var released = semaphore.Release();
        Assert.Equal(1, released);
        Assert.True(semaphore.TryWait());
    }

    [Fact]
    public async Task Release_WakesQueuedWaitersInOrder()
    {
        var semaphore = new JobSemaphore(initialCount: 1);

        Assert.True(semaphore.TryWait());

        var firstWait = semaphore.WaitAsync();
        var secondWait = semaphore.WaitAsync();

        await Task.Delay(50);
        Assert.False(firstWait.IsCompleted);
        Assert.False(secondWait.IsCompleted);

        semaphore.Release();
        await firstWait.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(secondWait.IsCompleted);

        semaphore.Release();
        await secondWait.WaitAsync(TimeSpan.FromSeconds(1));
    }
}
