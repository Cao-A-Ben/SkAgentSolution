using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Memory
{
    /// <summary>
    /// 【Memory 层 - 内存版短期记忆实现】
    /// IShortTermMemory 的内存实现，线程安全且固定容量。
    /// 使用 ConcurrentDictionary + LinkedList 实现每个会话独立的固定长度记忆队列。
    /// 通过 DI 以单例注册，默认每个会话最多保留 20 条记录。
    /// 后续可替换为 Redis/数据库实现以支持跨进程持久化。
    /// </summary>
    public sealed class InMemoryShortTermMemory : IShortTermMemory
    {
        /// <summary>
        /// 内存存储字典，键为会话 ID，值为该会话的回合记录链表。
        /// 使用 ConcurrentDictionary 保证多请求并发时的线程安全。
        /// </summary>
        private readonly ConcurrentDictionary<string, LinkedList<TurnRecord>> _store = new();

        /// <summary>
        /// 每个会话的最大记录数，超出时自动淘汰最旧记录（FIFO）。
        /// </summary>
        private readonly int _maxPerConversation;

        /// <summary>
        /// 初始化内存短期记忆存储。
        /// </summary>
        /// <param name="maxPerConversation">每个会话的最大记录数，默认 20。</param>
        public InMemoryShortTermMemory(int maxPerConversation = 20)
        {
            _maxPerConversation = maxPerConversation;
        }

        /// <summary>
        /// 向指定会话追加一条回合记录，如果超过容量则自动移除最旧的一条。
        /// </summary>
        public Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default)
        {
            // 1. 检查取消令牌
            ct.ThrowIfCancellationRequested();

            // 2. 获取或创建该会话的链表
            var list = _store.GetOrAdd(conversationId, _ => new LinkedList<TurnRecord>());

            // 3. 加锁操作链表（LinkedList 非线程安全）
            lock (list)
            {
                // 4. 追加新记录到末尾
                list.AddLast(record);

                // 5. 如果超过最大容量，移除最旧的一条（FIFO）
                if (list.Count > _maxPerConversation)
                {
                    list.RemoveFirst();
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取指定会话的最近 N 条回合记录。
        /// 返回的列表按时间倒序排列（最新的在前）。
        /// </summary>
        public Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default)
        {
            // 1. 检查取消令牌
            ct.ThrowIfCancellationRequested();

            // 2. 若会话不存在则返回空列表
            if (!_store.TryGetValue(conversationId, out var list))
                return Task.FromResult<IReadOnlyList<TurnRecord>>(Array.Empty<TurnRecord>());

            // 3. 确保 take 非负数
            take = Math.Max(0, take);

            // 4. 加锁读取链表，取最后 N 条（倒序取前 N 条）
            List<TurnRecord> result;
            lock (list)
            {
                result = list.Reverse().Take(take).ToList();
            }
            return Task.FromResult<IReadOnlyList<TurnRecord>>(result);
        }

        /// <summary>
        /// 清空指定会话的所有短期记忆记录。
        /// </summary>
        public Task ClearAsync(string conversationId, CancellationToken ct = default)
        {
            // 1. 检查取消令牌
            ct.ThrowIfCancellationRequested();

            // 2. 从存储中移除该会话的链表
            _store.TryRemove(conversationId, out _);

            return Task.CompletedTask;
        }
    }
}
