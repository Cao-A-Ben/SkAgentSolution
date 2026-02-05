using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Planning;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Runtime
{
    public sealed class AgentRunContext
    {
        public string RunId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 关键: 与Router/Agents 统一的上下文 同一个State会贯穿整个Run
        /// </summary>
        public AgentContext RootContext { get; set; }

        //目标 来自Planner
        public string Goal { get; init; } = string.Empty;
        //当前计划
        public AgentPlan? Plan { get; set; }


        public IList<PlanStepExecution> Steps { get; } = new List<PlanStepExecution>();
        ////最终结构 唯一可信
        //public PlanExecutionResult? FinalResult { get; set; }

        public AgentRunStatus Status { get; set; } = AgentRunStatus.Initialized;


        public AgentRunContext(AgentContext context, string goal, AgentPlan plan)
        {
            RootContext = context ?? throw new ArgumentNullException(nameof(context));
            Goal = goal ?? string.Empty;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));

        }
    }
}
