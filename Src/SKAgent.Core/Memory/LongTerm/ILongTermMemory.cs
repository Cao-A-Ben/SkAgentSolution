namespace SKAgent.Core.Memory.LongTerm;

/// <summary>
/// 长期记忆读写契约。
/// </summary>
public interface ILongTermMemory
{
    Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default);

    Task<LongTermUpsertResult> UpsertAsync(
        IReadOnlyList<LongTermMemoryWrite> writes,
        CancellationToken ct = default);
}
