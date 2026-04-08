namespace SKAgent.Core.Memory.ShortTerm;

public interface IRecentConversationHistory
{
    Task<IReadOnlyList<TurnRecord>> GetRecentAsync(
        string conversationId,
        int take,
        CancellationToken ct = default);
}
