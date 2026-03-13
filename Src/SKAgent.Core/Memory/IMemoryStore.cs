using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Memory;

namespace SKAgent.Agents.Memory
{
    /// <summary>
    /// 统一的 Memory Store 抽象（可选）。
    /// W6-2 暂不使用，后续可用于统一 Short/Working/Long 的适配。
    /// </summary>
    public interface IMemoryStore
    {
        Task AppendAsync(string conversationId, MemoryItem item, CancellationToken ct = default);
        Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default);
        Task ClearAsync(string conversationId, CancellationToken ct = default);
    }
}
