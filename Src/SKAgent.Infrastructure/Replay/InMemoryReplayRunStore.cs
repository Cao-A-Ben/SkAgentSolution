using System.Collections.Concurrent;
using SKAgent.Core.Replay;

namespace SKAgent.Infrastructure.Replay;

public sealed class InMemoryReplayRunStore : IReplayRunStore
{
    private readonly ConcurrentDictionary<string, ReplayRunRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(ReplayRunRecord record, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _records[record.RunId] = record;
        return Task.CompletedTask;
    }

    public Task<ReplayRunRecord?> GetAsync(string runId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _records.TryGetValue(runId, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<ReplayRunRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var items = _records.Values
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(Math.Max(1, take))
            .ToList();

        return Task.FromResult<IReadOnlyList<ReplayRunRecord>>(items);
    }
}
