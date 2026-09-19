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
    private bool _disposed;

    public async Task<T> RunAsync<T>(Func<T> work, TimeSpan hardTimeout, CancellationToken ct)
    {
        BlockingCollection<Action> queue;
        int generation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _queue ??= StartWorker();
            queue = _queue;
            generation = _generation;
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.Add(() =>
        {
            bool stale;
            lock (_gate)
            {
                stale = generation != _generation;
            }
            try
            {
                var result = work();
                if (stale)
                {
                    (result as IDisposable)?.Dispose();
                }
                else
                {
                    tcs.TrySetResult(result);
                }
            }
            catch (Exception exception) when (!stale)
            {
                tcs.TrySetException(exception);
            }
            catch
            {
                // Stale worker: swallow — nobody is listening.
            }
        });

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
        var queue = new BlockingCollection<Action>();
        var thread = new Thread(() =>
        {
            foreach (var work in queue.GetConsumingEnumerable())
            {
                work();
            }
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
