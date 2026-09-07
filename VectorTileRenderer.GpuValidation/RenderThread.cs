using System.Collections.Concurrent;

namespace WuGing.VectorTileRenderer.GpuValidation;

// Pumps captured await continuations on the WGL owner. No background native drawing.
internal sealed class RenderThread : SynchronizationContext, IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();

    public override void Post(SendOrPostCallback callback, object? state) => queue.Add((callback, state));

    public T Run<T>(Func<Task<T>> action)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            var task = action();
            // Wake the pump even when completion did not capture this context.
            _ = task.ContinueWith(_ => Post(_ => { }, null), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            while (!task.IsCompleted)
            {
                if (!queue.TryTake(out var work, TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("Render continuation did not complete within 30 seconds.");
                }
                work.Callback(work.State);
            }
            return task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    public void Dispose() => queue.Dispose();
}
