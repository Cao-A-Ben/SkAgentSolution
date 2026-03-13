using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Memory.ShortTerm;

namespace SKAgent.Core.Memory.Working
{
    /// <summary>
    /// 工作记忆存储契约。
    /// 工作记忆用于保存会话进行中、短时间高频复用的中间事实。
    /// </summary>
    public interface IWorkingMemoryStore
    {
        /// <summary>
        /// 获取会话的全部工作记忆条目。
        /// </summary>
        Task<IReadOnlyList<MemoryItem>> ListAsync(string conversationId, CancellationToken ct = default);

        /// <summary>
        /// 向会话追加一条工作记忆条目。
        /// </summary>
        Task AppendAsync(string conversationId, MemoryItem item, CancellationToken ct = default);

        /// <summary>
        /// 清空会话工作记忆。
        /// </summary>
        Task ClearAsync(string conversationId, CancellationToken ct = default);
    }
}
