using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Memory
{
    /// <summary>
    /// 【Memory 层 - 短期记忆接口】
    /// 定义会话级短期记忆的读写契约，用于存储和检索每次对话回合的记录。
    /// 短期记忆为最近 N 轮对话，供 PlannerAgent 和 ChatAgent 参考上下文。
    /// 
    /// 在运行时流程中的使用：
    /// - AgentRuntimeService.RunAsync 开始时通过 GetRecentAsync 加载最近回合。
    /// - AgentRuntimeService.CommitShortTermMemoryAsync 在回合结束后通过 AppendAsync 写入新记录。
    /// </summary>
    public interface IShortTermMemory
    {
        /// <summary>
        /// 向指定会话的记忆序列末尾追加一条回合记录。
        /// 实现应保证固定容量（超出时自动淘汰最旧记录）。
        /// </summary>
        /// <param name="conversationId">会话唯一标识。</param>
        /// <param name="record">本次回合的完整记录。</param>
        /// <param name="ct">取消令牌。</param>
        Task AppendAsync(string conversationId, TurnRecord record, CancellationToken ct = default);

        /// <summary>
        /// 获取指定会话的最近 N 条回合记录，按时间倒序返回。
        /// 用于注入 ConversationState["recent_turns"] 供 Planner 和 ChatAgent 使用。
        /// </summary>
        /// <param name="conversationId">会话唯一标识。</param>
        /// <param name="take">最多返回的记录数。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>最近的回合记录列表。</returns>
        Task<IReadOnlyList<TurnRecord>> GetRecentAsync(string conversationId, int take, CancellationToken ct = default);

        /// <summary>
        /// 清空指定会话的所有短期记忆。
        /// </summary>
        /// <param name="conversationId">会话唯一标识。</param>
        /// <param name="ct">取消令牌。</param>
        Task ClearAsync(string conversationId, CancellationToken ct = default);
    }
}
