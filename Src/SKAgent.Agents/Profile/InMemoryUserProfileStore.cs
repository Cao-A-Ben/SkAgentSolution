using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Profile
{
    /// <summary>
    /// 【Profile 层 - 内存版用户画像存储实现】
    /// IUserProfileStore 的内存实现，使用 ConcurrentDictionary 保证线程安全。
    /// 通过 DI 以单例注册，进程生命周期内持久化画像数据。
    /// 后续可替换为 Redis/数据库实现以支持跨进程持久化。
    /// </summary>
    public sealed class InMemoryUserProfileStore : IUserProfileStore
    {
        /// <summary>
        /// 内存存储字典，键为会话ID，值为该会话的画像键值对。
        /// 使用 ConcurrentDictionary 确保多请求并发时的线程安全。
        /// </summary>
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _store = new();

        /// <summary>
        /// 根据会话 ID 获取画像副本。
        /// 返回的是深拷贝，避免外部修改影响内部存储。
        /// </summary>
        public Task<Dictionary<string, string>> GetAsync(string conversationId, CancellationToken ct = default)
        {
            // 1. 检查取消令牌
            ct.ThrowIfCancellationRequested();

            // 2. 尝试从存储中获取，返回深拷贝；若不存在则返回空字典
            if (_store.TryGetValue(conversationId, out var dict))
            {
                return Task.FromResult(new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase));
            }
            return Task.FromResult(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 向指定会话的画像中合并写入新字段。
        /// 使用 ConcurrentDictionary.AddOrUpdate 保证原子性：
        /// - 若会话无画像：创建新字典。
        /// - 若会话已有画像：将 patch 中的字段逐个覆盖/追加到已有字典。
        /// </summary>
        public Task UpsertAsync(string conversationId, Dictionary<string, string> patch, CancellationToken ct = default)
        {
            // 1. 检查取消令牌
            ct.ThrowIfCancellationRequested();

            // 2. 原子性地新增或更新画像字典
            _store.AddOrUpdate(conversationId,
                _ => new Dictionary<string, string>(patch, StringComparer.OrdinalIgnoreCase),
                (_, existing) =>
                {
                    foreach (var kv in patch) existing[kv.Key] = kv.Value;
                    return existing;
                });

            return Task.CompletedTask;
        }
    }
}
