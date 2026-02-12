using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Agent
{
    /// <summary>
    /// 【Core 抽象层 - Agent 接口】
    /// 所有 Agent 的顶层抽象接口，定义了 Agent 的最小契约。
    /// 任何可被路由执行的 Agent（如 SKChatAgent、McpAgent）都必须实现此接口。
    /// RouterAgent 依赖此接口进行名称匹配和分发调用。
    /// </summary>
    public interface IAgent
    {
        /// <summary>
        /// Agent 的唯一标识名称，用于路由匹配。
        /// RouterAgent 在接收到 PlanStep.Agent 指定的目标名称后，
        /// 会通过此属性在已注册的 IAgent 集合中查找匹配项。
        /// 例如: "chat"、"mcp"。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 异步执行 Agent 的核心逻辑。
        /// 由 RouterAgent 在计划执行阶段调用，传入当前步骤的独立上下文 (StepContext)。
        /// 每个 Agent 根据上下文中的 Input、State 等信息完成各自的业务逻辑，
        /// 并返回统一的 <see cref="AgentResult"/> 结果。
        /// </summary>
        /// <param name="context">
        /// 当前步骤的执行上下文，包含输入指令、路由目标、共享状态等信息。
        /// 由 PlanExecutor.CreateStepContext 构建，独立于 Root 上下文，避免步骤间状态污染。
        /// </param>
        /// <returns>Agent 执行结果，包含输出文本、是否成功、以及可选的下一跳 Agent 名称。</returns>
        Task<AgentResult> ExecuteAsync(AgentContext context);
    }
}
