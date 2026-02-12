using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Execution
{
    /// <summary>
    /// 【Execution 层 - 计划执行结果模型】
    /// 表示整个执行计划的结果汇总，包含目标和所有步骤的执行结果。
    /// 当前版本为预留模型，实际结果直接通过 AgentRunContext 传递。
    /// </summary>
    public sealed class PlanExecutionResult
    {
        /// <summary>
        /// 计划目标描述，对应 AgentPlan.Goal。
        /// </summary>
        public string Goal { get; init; } = string.Empty;

        /// <summary>
        /// 所有步骤的执行结果列表。
        /// </summary>
        public IReadOnlyList<StepExecutionResult> Steps { get; init; } = Array.Empty<StepExecutionResult>();
    }
}
