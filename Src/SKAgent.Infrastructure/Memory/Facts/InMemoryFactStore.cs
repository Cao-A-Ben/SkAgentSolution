using System.Collections.Concurrent;
using SKAgent.Core.Memory.Facts;

namespace SKAgent.Infrastructure.Memory.Facts;

public sealed class InMemoryFactStore : IFactStore
{
    private readonly ConcurrentDictionary<string, Dictionary<string, FactRecord>> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<FactRecord>> ListAsync(string conversationId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(conversationId, out var map))
            return Task.FromResult<IReadOnlyList<FactRecord>>([]);

        lock (map)
        {
            return Task.FromResult<IReadOnlyList<FactRecord>>(map.Values.OrderByDescending(x => x.Ts).ToList());
        }
    }

    public Task<FactConflictDecision> UpsertAsync(string conversationId, FactRecord fact, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var map = _store.GetOrAdd(conversationId, _ => new Dictionary<string, FactRecord>(StringComparer.OrdinalIgnoreCase));
        lock (map)
        {
            map.TryGetValue(fact.Key, out var existing);
            var decision = Resolve(existing, fact);

            if (decision.Action == FactConflictAction.Upserted)
                map[fact.Key] = fact;

            return Task.FromResult(decision);
        }
    }

    public Task ClearAsync(string conversationId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _store.TryRemove(conversationId, out _);
        return Task.CompletedTask;
    }

    private static FactConflictDecision Resolve(FactRecord? existing, FactRecord incoming)
    {
        if (existing is null)
        {
            return new FactConflictDecision(
                incoming.Key,
                FactConflictAction.Upserted,
                ExistingValue: null,
                IncomingValue: incoming.Value,
                Reason: "new_fact");
        }

        if (incoming.Confidence > existing.Confidence || incoming.Ts >= existing.Ts)
        {
            return new FactConflictDecision(
                incoming.Key,
                FactConflictAction.Upserted,
                ExistingValue: existing.Value,
                IncomingValue: incoming.Value,
                Reason: "higher_confidence_or_newer");
        }

        return new FactConflictDecision(
            incoming.Key,
            FactConflictAction.Skipped,
            ExistingValue: existing.Value,
            IncomingValue: incoming.Value,
            Reason: "lower_confidence");
    }
}
