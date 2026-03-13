namespace SKAgent.Core.Memory.ShortTerm;

/// <summary>
/// 会话级短期记忆读写契约。
/// </summary>
public interface IShortTermMemory
{
    Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default);

    Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default);

    Task ClearAsync(string conversationId, CancellationToken ct = default);
}
