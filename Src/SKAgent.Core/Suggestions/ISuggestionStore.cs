namespace SKAgent.Core.Suggestions;

public interface ISuggestionStore
{
    Task<SuggestionRecord?> GetAsync(DateOnly date, string conversationId, CancellationToken ct = default);

    Task<SuggestionRecord?> GetByRunIdAsync(string runId, CancellationToken ct = default);

    Task SaveAsync(SuggestionRecord record, CancellationToken ct = default);

    Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default);
}
