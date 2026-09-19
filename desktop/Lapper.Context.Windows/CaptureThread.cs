using System.Collections.Concurrent;

namespace Lapper.Context.Windows;

public sealed class CaptureTimeoutException : Exception
{
    public CaptureTimeoutException()
        : base("Context capture exceeded its time budget.")
    {
    }
}

/// <summary>
/// Dedicated MTA worker for all UI Automation COM calls. UIA must never run
/// on the WinUI STA thread (deadlock/re-entrancy), and UIA calls block
/// uncancellably — so a hung provider "poisons" the thread: the next call
/// lazily spins up a replacement while the stale thread discards its
/// results when it eventually returns.
/// </summary>
public sealed class CaptureThread : IDisposable
{
    private readonly Lock _gate = new();
    private BlockingCollection<Action>? _queue;
    private int _generation;
    private int _generationOfQueue;
    private bool _disposed;

    public async Task<T> RunAsync<T>(Func<T> work, TimeSpan hardTimeout, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enqueuedGeneration = 0;

        bool IsStale()
        {
            // A caller that stopped waiting (timeout already thrown) counts
            // as stale even before a Poison lands.
            if (tcs.Task.IsCompleted)
            {
                return true;
            }
            lock (_gate)
            {
                return enqueuedGeneration != _generation;
            }
        }

        var item = () =>
        {
            // Skip work queued behind a hang: its caller gets a fast timeout
            // instead of waiting out its own hard-timeout window.
            if (IsStale())
            {
                tcs.TrySetException(new CaptureTimeoutException());
                return;
            }
            try
            {
                var result = work();
                // Staleness must be re-read AFTER work(): the item that
                // caused a Poison() is only stale once it returns, and its
                // result must be disposed, never published.
                if (IsStale() || !tcs.TrySetResult(result))
                {
                    (result as IDisposable)?.Dispose();
                }
            }
            catch (Exception exception)
            {
                tcs.TrySetException(exception);
            }
        };

        // Enqueue under the gate so Poison/Dispose (which CompleteAdding
        // under the same gate) cannot race the Add. One retry: a fresh
        // worker replaces one poisoned between snapshot and enqueue.
        for (var attempt = 0; ; attempt++)
        {
            var enqueued = false;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _queue ??= StartWorker();
                if (_generationOfQueue == _generation)
                {
                    enqueuedGeneration = _generation;
                    _queue.Add(item);
                    enqueued = true;
                }
            }
            if (enqueued)
            {
                break;
            }
            if (attempt >= 1)
            {
                throw new CaptureTimeoutException();
            }
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(hardTimeout, ct)).ConfigureAwait(false);
        if (completed != tcs.Task)
        {
            ct.ThrowIfCancellationRequested();
            Poison();
            throw new CaptureTimeoutException();
        }
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>Abandons the current worker; the next RunAsync starts a fresh one.</summary>
    public void Poison()
    {
        lock (_gate)
        {
            _generation++;
            _queue?.CompleteAdding();
            _queue = null;
        }
    }

    private BlockingCollection<Action> StartWorker()
    {
        _generationOfQueue = _generation; // caller holds _gate
        var queue = new BlockingCollection<Action>();
        var thread = new Thread(() =>
        {
            foreach (var work in queue.GetConsumingEnumerable())
            {
                work();
            }
            // Safe: Add only happens under _gate while this queue is
            // current and not complete, so no producer can race this.
            queue.Dispose();
        })
        {
            IsBackground = true,
            Name = "Lapper.Capture",
        };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return queue;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _queue?.CompleteAdding();
            _queue = null;
        }
    }
}
