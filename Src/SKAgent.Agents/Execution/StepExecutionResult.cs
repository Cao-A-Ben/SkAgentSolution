using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Execution
{
    /// <summary>
    /// 【Execution 层 - 步骤执行结果模型】
    /// 表示单个 PlanStep 的执行结果，嵌套在 PlanExecutionResult.Steps 中。
    /// 当前版本为预留模型，实际结果通过 PlanStepExecution 传递。
    /// </summary>
    public class StepExecutionResult
    {
        /// <summary>
        /// 步骤执行顺序，对应 PlanStep.Order。
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// 执行该步骤的 Agent 名称。
        /// </summary>
        public string Agent { get; init; } = string.Empty;

        /// <summary>
        /// 该步骤的输入内容。
        /// </summary>
        public string Input { get; set; } = string.Empty;

        /// <summary>
        /// 该步骤的输出内容。
        /// </summary>
        public string Output { get; set; } = string.Empty;
    }
}
