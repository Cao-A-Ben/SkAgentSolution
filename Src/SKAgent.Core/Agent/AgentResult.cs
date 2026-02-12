using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Agent
{
    /// <summary>
    /// 【Core 抽象层 - Agent 统一输出结果】
    /// 所有 IAgent.ExecuteAsync 的返回类型，封装 Agent 的执行产出。
    /// PlanExecutor 根据此结果判断步骤是否成功、是否需要动态路由到下一个 Agent。
    /// </summary>
    public sealed class AgentResult
    {
        /// <summary>
        /// Agent 的文本输出内容。
        /// 对于 SKChatAgent，这是 LLM 生成的回复文本；
        /// 对于 McpAgent，这是外部协议调用的返回结果。
        /// 所有步骤的 Output 最终由 PlanExecutor 拼接为 run.FinalOutput。
        /// </summary>
        public string Output { get; init; } = string.Empty;

        /// <summary>
        /// 标识本次 Agent 执行是否成功。
        /// 如果为 false，PlanExecutor 会立即终止当前 Run 并标记为 Failed。
        /// </summary>
        public bool IsSuccess { get; init; } = true;

        /// <summary>
        /// 可选：动态路由到下一个 Agent 的名称。
        /// 如果非空，PlanExecutor 会将其写入 ConversationState["next_agent_override"]，
        /// 后续步骤的目标 Agent 会被覆盖为此值（实现动态路由）。
        /// 当前版本为预留扩展，暂未在具体 Agent 中使用。
        /// </summary>
        public string? NextAgent { get; init; }
    }
}
