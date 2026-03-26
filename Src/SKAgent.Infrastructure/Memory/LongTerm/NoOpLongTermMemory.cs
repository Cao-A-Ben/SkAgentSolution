using SKAgent.Core.Memory;
using SKAgent.Core.Memory.LongTerm;

namespace SKAgent.Infrastructure.Memory.LongTerm
{
    /// <summary>
    /// 长期记忆的占位实现（No-Op）。
    /// 当前阶段不做真实检索与落库，用于保持依赖完整与运行链路稳定。
    /// </summary>
    public sealed class NoOpLongTermMemory : ILongTermMemory
    {
        public Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MemoryItem>>(Array.Empty<MemoryItem>());

        public Task<LongTermUpsertResult> UpsertAsync(
            IReadOnlyList<LongTermMemoryWrite> writes,
            CancellationToken ct = default)
            => Task.FromResult(new LongTermUpsertResult(Inserted: 0, DedupeCount: writes.Count));
    }
}
