using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Profile
{
    /// <summary>
    /// 【Profile 层 - 用户画像存储接口】
    /// 定义用户画像（User Profile）的读写契约。
    /// 画像是比短期记忆更稳定的用户信息（如姓名、地点、作息习惯），
    /// 跨会话持久化，用于个性化回复和建议。
    /// 
    /// 在运行时流程中的使用：
    /// - AgentRuntimeService.RunAsync 开始时通过 GetAsync 读取当前画像。
    /// - AgentRuntimeService.RunAsync 结束时通过 UpsertAsync 写入新提取的画像字段。
    /// </summary>
    public interface IUserProfileStore
    {
        /// <summary>
        /// 根据会话 ID 获取用户画像字典。
        /// 若该会话无画像记录，返回空字典。
        /// </summary>
        /// <param name="conversationId">会话唯一标识。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>画像键值对字典，常见 key: name、location、sleep 等。</returns>
        Task<Dictionary<string, string>> GetAsync(string conversationId, CancellationToken ct = default);

        /// <summary>
        /// 向指定会话的画像中合并写入新字段（已有 key 覆盖，新 key 追加）。
        /// 由 AgentRuntimeService 在回合结束后调用，将 ProfileExtractor 提取的 patch 写入存储。
        /// </summary>
        /// <param name="conversationId">会话唯一标识。</param>
        /// <param name="patch">待合并的画像字段（增量更新）。</param>
        /// <param name="ct">取消令牌。</param>
        Task UpsertAsync(string conversationId, Dictionary<string, string> patch, CancellationToken ct = default);
    }
}
