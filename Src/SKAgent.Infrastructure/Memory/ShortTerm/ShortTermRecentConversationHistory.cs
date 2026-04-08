using SKAgent.Core.Memory.ShortTerm;

namespace SKAgent.Infrastructure.Memory.ShortTerm;

public sealed class ShortTermRecentConversationHistory : IRecentConversationHistory
{
    private readonly IShortTermMemory _shortTermMemory;

    public ShortTermRecentConversationHistory(IShortTermMemory shortTermMemory)
    {
        _shortTermMemory = shortTermMemory;
    }

    public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(
        string conversationId,
        int take,
        CancellationToken ct = default)
        => _shortTermMemory.GetRecentAsync(conversationId, take, ct);
}
