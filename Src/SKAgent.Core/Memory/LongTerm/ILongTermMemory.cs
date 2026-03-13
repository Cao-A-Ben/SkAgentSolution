using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Memory.LongTerm
{
    public interface ILongTermMemory
    {
        Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default);
    }
}
