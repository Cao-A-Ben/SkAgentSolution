using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具调用器接口】
    /// 负责根据 ToolInvocation 查找并执行对应工具，封装超时、异常处理和性能度量。
    /// PlanExecutor 在执行 Kind=Tool 的 PlanStep 时通过此接口发起工具调用。
    /// </summary>
    public interface IToolInvoker
    {
        /// <summary>
        /// 根据调用描述执行工具。
        /// </summary>
        /// <param name="invocation">工具调用描述，包含 RunId、StepId、工具名和参数。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>工具执行结果。</returns>
        Task<ToolResult> InvokeAsync(ToolInvocation invocation, CancellationToken ct);
    }
}
