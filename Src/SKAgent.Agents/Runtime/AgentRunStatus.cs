using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Runtime
{
    /// <summary>
    /// 【Runtime 层 - 运行状态枚举】
    /// 表示单次 AgentRunContext 的生命周期状态。
    /// 由 AgentRuntimeService 和 PlanExecutor 在流程各阶段设置。
    /// </summary>
    public enum AgentRunStatus
    {
        /// <summary>初始化完成，尚未开始规划。</summary>
        Initialized,

        /// <summary>计划已生成，等待执行。</summary>
        Planned,

        /// <summary>计划正在执行中。</summary>
        Executing,

        /// <summary>所有步骤执行完成。</summary>
        Completed,

        /// <summary>执行过程中发生失败。</summary>
        Failed
    }
}
