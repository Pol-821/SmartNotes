using System.Collections.Concurrent;
using SmartNotes.Api.Models;

namespace SmartNotes.Api.Services;

public class TranscriptionQueue
{
    private readonly ConcurrentQueue<TranscriptionJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(TranscriptionJob job)
    {
        _queue.Enqueue(job);
        _signal.Release();
    }

    public Task EnqueueAsync(TranscriptionJob job)
    {
        Enqueue(job);
        return Task.CompletedTask;
    }

    public async Task<TranscriptionJob> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        _queue.TryDequeue(out var job);
        return job!;
    }
}