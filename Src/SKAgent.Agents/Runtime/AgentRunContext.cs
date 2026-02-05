using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Planning;

namespace SKAgent.Agents.Runtime
{
    public sealed class AgentRunContext
    {
        public string RunId { get; } = Guid.NewGuid().ToString("N");

        //目标 来自Planner
        public string Goal { get; init; } = string.Empty;
        //当前计划
        public AgentPlan? Plan { get; set; }


        public IList<PlanStepExecution> StepExecutions { get; } = new List<PlanStepExecution>();
        //最终结构 唯一可信
        public PlanExecutionResult? FinalResult { get; set; }

        public AgentRunStatus Status { get; set; } = AgentRunStatus.Initialized;
    }
}
