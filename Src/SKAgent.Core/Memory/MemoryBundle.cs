using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Memory
{
    /// <summary>
    /// 记忆聚合模型。
    /// 用于将 recent-history、short-term、working、long-term 统一传递给 Prompt 组合器。
    /// </summary>
    public sealed record MemoryBundle(
        IReadOnlyList<MemoryItem> RecentHistory,
        IReadOnlyList<MemoryItem> ShortTerm,
        IReadOnlyList<MemoryItem> Working,
        IReadOnlyList<MemoryItem> LongTerm
    );

}
