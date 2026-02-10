using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Memory
{
    //线程安全 固定容量
    public sealed class InMemoryShortTermMemory : IShortTermMemory
    {
        // 存储对话记录的字典，键为对话ID，值为对应的记录链表
        private readonly ConcurrentDictionary<string, LinkedList<TurnRecord>> _store = new();

        // 每个对话的最大记录数
        private readonly int _maxPerConversation;


        public InMemoryShortTermMemory(int maxPerConversation = 20)
        {
            _maxPerConversation = maxPerConversation;
        }


        public Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var list = _store.GetOrAdd(conversationId, _ => new LinkedList<TurnRecord>());

            lock (list)
            {
                list.AddLast(record);
                if (list.Count > _maxPerConversation)
                {
                    list.RemoveFirst();
                }
            }


            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!_store.TryGetValue(conversationId, out var list))
                return Task.FromResult<IReadOnlyList<TurnRecord>>(Array.Empty<TurnRecord>());

            take = Math.Max(0, take);

            List<TurnRecord> result;
            lock (list)
            {
                result = list.Reverse().Take(take).ToList();
            }
            return Task.FromResult<IReadOnlyList<TurnRecord>>(result);
        }

        public Task ClearAsync(string conversationId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            _store.TryRemove(conversationId, out _);

            return Task.CompletedTask;
        }
    }
}
