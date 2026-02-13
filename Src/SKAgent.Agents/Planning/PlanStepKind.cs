using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Planning
{
    /// <summary>
    /// 【Planning 层 - 计划步骤类型】
    /// 区分 PlanStep 的执行方式：Agent 路由执行或 Tool 直接调用。
    /// PlanExecutor 根据此值走不同的执行分支。
    /// </summary>
    public enum PlanStepKind
    {
        /// <summary>由 RouterAgent 路由到目标 Agent 执行（chat/mcp 等）。</summary>
        Agent = 0,
        /// <summary>由 ToolInvoker 直接调用已注册的工具执行。</summary>
        Tool = 1
    }
}
