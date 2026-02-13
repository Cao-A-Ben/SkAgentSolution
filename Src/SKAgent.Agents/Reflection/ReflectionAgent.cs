using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;

namespace SKAgent.Agents.Reflection
{
    public class ReflectionAgent : IReflectionAgent
    {
        public Task<ReflectionDecision> DecideAsync(AgentRunContext run, PlanStep step, string reason, CancellationToken ct)
        {

            //最小策略：线虫是同一步（由 RetryPolicy 限制次数）
            return Task.FromResult(new ReflectionDecision(ReflectionDecisionKind.RetrySameStep, reason));
        }
    }
}
