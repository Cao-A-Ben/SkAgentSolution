using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Planning
{
    public sealed class AgentPlan
    {
        public string Goal { get; init; } = string.Empty;

        public IReadOnlyList<PlanStep> Steps { get; init; } = Array.Empty<PlanStep>();
    }
}
