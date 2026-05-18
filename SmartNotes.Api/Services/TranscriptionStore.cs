using SmartNotes.Api.Models;
using System.Collections.Concurrent;

namespace SmartNotes.Api.Services;

public class TranscriptionStore
{
    private readonly ConcurrentDictionary<string, TranscriptionJob> _jobs = new();

    public TranscriptionJob CreateJob()
    {
        var job = new TranscriptionJob();
        _jobs.TryAdd(job.Id, job);
        return job;
    }

    public TranscriptionJob? Get(string id)
        => _jobs.TryGetValue(id, out var job) ? job : null;

    public void Update(TranscriptionJob job)
        => _jobs.AddOrUpdate(job.Id, job, (_, existingJob) => job);

    public IEnumerable<TranscriptionJob> GetAll()
        => _jobs.Values;
}