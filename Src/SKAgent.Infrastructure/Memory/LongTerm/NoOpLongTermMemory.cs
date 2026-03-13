using System;
using System.Collections.Generic;
using System.Text;
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
        /// <summary>
        /// 返回空结果，表示当前无长期记忆召回。
        /// </summary>
        public Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MemoryItem>>(Array.Empty<MemoryItem>());

        /// <summary>
        /// 占位实现：忽略写入请求。
        /// </summary>
        public Task UpsertAsync(IEnumerable<MemoryItem> items, CancellationToken ct = default)
            => Task.CompletedTask; // 如果你的接口没有 Upsert，就删掉这行
    }
}
