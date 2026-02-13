using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;

namespace SKAgent.Agents.Reflection
{
    public interface IReflectionAgent
    {
        Task<ReflectionDecision> DecideAsync(AgentRunContext run, PlanStep step, string reason, CancellationToken ct);
    }


}
