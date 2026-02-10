using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Memory
{
    public interface IShortTermMemory
    {
        Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default);

        Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId,int take, CancellationToken ct = default);
        Task ClearAsync(string conversationId, CancellationToken ct = default);
    }
}
