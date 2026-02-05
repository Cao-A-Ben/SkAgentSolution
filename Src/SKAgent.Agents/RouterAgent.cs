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


            // 简化 从Context.state决定路由
            if (!context.State.TryGetValue("target", out var target))
            {
                throw new InvalidOperationException("No target agent.");
            }

            if (!_agents.TryGetValue(target.ToString()!, out var agent))
            {
                throw new InvalidOperationException("Target agent not registered.");
            }

            var agentName = agent.Name;
            _logger.LogInformation(
                "Routing to agent {AgentName}, RequestId={RequestId}",
                agentName,
                context.RequestId);

            return await agent.ExecuteAsync(context);
        }


   
    }
}
