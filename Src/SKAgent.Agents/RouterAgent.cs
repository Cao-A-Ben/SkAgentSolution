using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Logging;
using SKAgent.Core.Agent;

namespace SKAgent.Agents
{
    public class RouterAgent : IAgent
    {
        private readonly Dictionary<string, IAgent> _agents;
        private readonly ILogger<RouterAgent> _logger;

        public string Name => "router";

        public RouterAgent(IEnumerable<IAgent> agents, ILogger<RouterAgent> logger)
        {
            _agents = agents.ToDictionary(agent => agent.Name, a => a, StringComparer.OrdinalIgnoreCase);
            _logger = logger;
        }

        public async Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            // 优先使用标准字段
            var target = context.Target;
            // ✅ 兼容旧实现（过渡）
            if (string.IsNullOrWhiteSpace(target) &&
                context.State.TryGetValue("target", out var legacyTarget))
            {
                target = legacyTarget?.ToString();
            }

            if (string.IsNullOrWhiteSpace(target))
                throw new InvalidOperationException("No target agent.");

            if (!_agents.TryGetValue(target, out var agent))
                throw new InvalidOperationException("Target agent not registered.");

            _logger.LogInformation(
                "Routing to agent {AgentName}, RequestId={RequestId}",
                agent.Name, context.RequestId);

            return await agent.ExecuteAsync(context);
        }




    }
}
