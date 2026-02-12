using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Logging;
using SKAgent.Core.Agent;

namespace SKAgent.Agents
{
    /// <summary>
    /// 【Agents 层 - 路由 Agent】
    /// 负责根据 AgentContext.Target 将请求分发到目标 Agent。
    /// 是 PlanExecutor 和具体业务 Agent 之间的桥梁。
    /// 
    /// 工作流程：
    /// PlanExecutor.ExecuteAsync → RouterAgent.ExecuteAsync(stepContext) → 目标 IAgent.ExecuteAsync
    /// 
    /// 路由逻辑：
    /// 1. 从 StepContext.Target 读取目标 Agent 名称。
    /// 2. 在已注册的 IAgent 字典中查找匹配项。
    /// 3. 调用目标 Agent 的 ExecuteAsync 并返回结果。
    /// </summary>
    public class RouterAgent : IAgent
    {
        /// <summary>已注册的 Agent 字典，键为 Agent.Name（忽略大小写）。</summary>
        private readonly Dictionary<string, IAgent> _agents;

        /// <summary>日志记录器，用于记录路由决策。</summary>
        private readonly ILogger<RouterAgent> _logger;

        /// <summary>Agent 名称，RouterAgent 自身不参与路由匹配。</summary>
        public string Name => "router";

        /// <summary>
        /// 初始化路由 Agent，将所有已注册的 IAgent 构建为名称字典。
        /// </summary>
        /// <param name="agents">DI 容器注入的所有 IAgent 实例。</param>
        /// <param name="logger">日志记录器。</param>
        public RouterAgent(IEnumerable<IAgent> agents, ILogger<RouterAgent> logger)
        {
            _agents = agents.ToDictionary(agent => agent.Name, a => a, StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        /// <summary>
        /// 根据 StepContext.Target 将请求路由到目标 Agent 执行。
        /// </summary>
        /// <param name="context">当前步骤的执行上下文，必须包含有效的 Target 字段。</param>
        /// <returns>目标 Agent 的执行结果。</returns>
        /// <exception cref="InvalidOperationException">当 Target 为空或目标 Agent 未注册时抛出。</exception>
        public async Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            // 1. 优先使用标准字段 Target
            var target = context.Target;

            // 2. 兼容旧实现：若 Target 为空，尝试从 State["target"] 读取（过渡期）
            if (string.IsNullOrWhiteSpace(target) &&
                context.State.TryGetValue("target", out var legacyTarget))
            {
                target = legacyTarget?.ToString();
            }

            // 3. 校验目标 Agent 名称不能为空
            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException("No target agent.");

            // 4. 在已注册字典中查找目标 Agent
            if (!_agents.TryGetValue(target, out var agent))
                throw new InvalidOperationException("Target agent not registered.");

            // 5. 记录路由日志
            var agentName = agent.Name;
            _logger.LogInformation(
                "Routing to agent {AgentName}, RequestId={RequestId}",
                agentName, context.RequestId);

            // 6. 调用目标 Agent 执行并返回结果
            return await agent.ExecuteAsync(context);
        }
    }
}
