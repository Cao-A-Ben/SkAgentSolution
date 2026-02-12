using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Runtime
{
    /// <summary>
    /// 【Runtime 层 - 步骤执行状态枚举】
    /// 表示单个 PlanStep 的执行状态，由 PlanExecutor 在执行过程中设置。
    /// </summary>
    public enum StepExecutionStatus
    {
        /// <summary>等待执行。</summary>
        Pending,

        /// <summary>正在执行中。</summary>
        Running,

        /// <summary>执行成功。</summary>
        Success,

        /// <summary>执行失败。</summary>
        Failed,
    }
}
