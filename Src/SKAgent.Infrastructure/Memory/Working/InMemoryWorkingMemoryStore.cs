using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.Working;

namespace SKAgent.Infrastructure.Memory.Working
{
    /// <summary>
    /// 工作记忆存储的内存实现。
    /// 以 conversationId 为键保存当前会话的临时工作记忆条目。
    /// </summary>
    public sealed class InMemoryWorkingMemoryStore : IWorkingMemoryStore
    {
        private readonly ConcurrentDictionary<string, List<MemoryItem>> _store = new();

        /// <summary>
        /// 获取会话当前全部工作记忆条目。
        /// </summary>
        public Task<IReadOnlyList<MemoryItem>> ListAsync(string conversationId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<MemoryItem>>(
                _store.TryGetValue(conversationId, out var list) ? list.ToList() : Array.Empty<MemoryItem>());
        }

        /// <summary>
        /// 追加一条工作记忆。
        /// </summary>
        public Task AppendAsync(string conversationId, MemoryItem item, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var list = _store.GetOrAdd(conversationId, _ => new List<MemoryItem>());
            lock (list) { list.Add(item); }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 清空会话的工作记忆。
        /// </summary>
        public Task ClearAsync(string conversationId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            _store.TryRemove(conversationId, out _);
            return Task.CompletedTask;
        }
    }
}
