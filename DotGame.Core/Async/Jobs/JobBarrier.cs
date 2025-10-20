using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotGame.Core.Async.Jobs;

/// <summary>
/// Multi-participant barrier that coordinates phased execution across jobs.
/// </summary>
public sealed class JobBarrier
{
    private readonly object gate = new();
    private TaskCompletionSource<bool> completion;
    private int participants;
    private int remaining;
    private int phase;

    public JobBarrier(int participantCount)
    {
        if (participantCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(participantCount));

        participants = participantCount;
        remaining = participantCount;
        completion = CreateCompletionSource();
    }

    /// <summary>
    /// Gets the total participant count for the barrier.
    /// </summary>
    public int ParticipantCount
    {
        get
        {
            lock (gate)
            {
                return participants;
            }
        }
    }

    /// <summary>
    /// Gets the number of participants that have not yet arrived in the current phase.
    /// </summary>
    public int RemainingParticipants
    {
        get
        {
            lock (gate)
            {
                return remaining;
            }
        }
    }

    /// <summary>
    /// Gets the current barrier phase number.
    /// </summary>
    public int Phase
    {
        get
        {
            lock (gate)
            {
                return phase;
            }
        }
    }

    /// <summary>
    /// Adds participants to the barrier for subsequent phases.
    /// </summary>
    public void AddParticipants(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        lock (gate)
        {
            participants += count;
            remaining += count;
        }
    }

    /// <summary>
    /// Removes participants from the barrier. If the removal satisfies the current phase, the barrier advances.
    /// </summary>
    public void RemoveParticipants(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        TaskCompletionSource<bool>? toRelease = null;

        lock (gate)
        {
            if (count > participants)
                throw new InvalidOperationException("Cannot remove more participants than are registered with the barrier.");

            participants -= count;
            remaining -= count;

            if (participants <= 0)
            {
                participants = 0;
                remaining = 0;
                toRelease = AdvancePhaseUnsafe();
                return;
            }

            if (remaining <= 0)
            {
                toRelease = AdvancePhaseUnsafe();
            }
        }

        toRelease?.TrySetResult(true);
    }

    /// <summary>
    /// Resets the barrier to a new participant count, abandoning the current phase.
    /// </summary>
    public void Reset(int participantCount)
    {
        if (participantCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(participantCount));

        TaskCompletionSource<bool>? toRelease;

        lock (gate)
        {
            participants = participantCount;
            remaining = participantCount;
            toRelease = AdvancePhaseUnsafe();
        }

    toRelease?.TrySetCanceled();
    }

    /// <summary>
    /// Signals arrival and waits for the remaining participants to reach the barrier.
    /// </summary>
    public void SignalAndWait(CancellationToken cancellationToken = default)
    {
        SignalAndWaitAsync(cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously signals arrival and waits for the remaining participants to reach the barrier.
    /// </summary>
    public async Task SignalAndWaitAsync(CancellationToken cancellationToken = default)
    {
        Task waitTask;
        TaskCompletionSource<bool>? toRelease = null;
        bool isLast = false;

        lock (gate)
        {
            if (participants == 0)
                throw new InvalidOperationException("Barrier has no registered participants.");

            if (remaining <= 0)
                remaining = participants;

            remaining--;
            waitTask = completion.Task;

            if (remaining <= 0)
            {
                toRelease = AdvancePhaseUnsafe();
                isLast = true;
            }
        }

        if (toRelease != null)
            toRelease.TrySetResult(true);

        if (!isLast)
            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private TaskCompletionSource<bool> AdvancePhaseUnsafe()
    {
        var current = completion;
        completion = CreateCompletionSource();
        remaining = participants;
        phase++;
        return current;
    }

    private static TaskCompletionSource<bool> CreateCompletionSource()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
