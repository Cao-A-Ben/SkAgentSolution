using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Memory
{
    /// <summary>
    /// 三层记忆聚合模型。
    /// 用于将 short-term、working、long-term 统一传递给 Prompt 组合器。
    /// </summary>
    public sealed record MemoryBundle(
        IReadOnlyList<MemoryItem> ShortTerm,
        IReadOnlyList<MemoryItem> Working,
        IReadOnlyList<MemoryItem> LongTerm
    );

}
