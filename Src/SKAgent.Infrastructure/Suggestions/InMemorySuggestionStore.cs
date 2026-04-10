using System.Collections.Concurrent;
using SKAgent.Core.Suggestions;

namespace SKAgent.Infrastructure.Suggestions;

public sealed class InMemorySuggestionStore : ISuggestionStore
{
    private readonly ConcurrentDictionary<string, SuggestionRecord> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<SuggestionRecord?> GetAsync(DateOnly date, string conversationId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _store.TryGetValue(BuildKey(date, conversationId), out var record);
        return Task.FromResult(record);
    }

    public Task SaveAsync(SuggestionRecord record, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _store[BuildKey(record.Date, record.ConversationId)] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var items = _store.Values
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, take))
            .ToList();

        return Task.FromResult<IReadOnlyList<SuggestionRecord>>(items);
    }

    private static string BuildKey(DateOnly date, string conversationId)
        => $"{date:yyyy-MM-dd}:{conversationId}";
}
