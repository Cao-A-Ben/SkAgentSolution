using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具调用描述】
    /// 封装一次工具调用的完整上下文信息，由 PlanExecutor 构建并传递给 IToolInvoker。
    /// </summary>
    /// <param name="RunId">所属运行的唯一标识，对应 AgentContext.RequestId。</param>
    /// <param name="StepId">所属步骤标识，对应 PlanStep.Order。</param>
    /// <param name="ToolName">目标工具名称，对应 PlanStep.Target（Kind=Tool）。</param>
    /// <param name="Arguments">工具调用参数，由 PlanStep.ArgumentsJson 解析而来。</param>
    public sealed record ToolInvocation(
        string RunId,
        string StepId,
        string ToolName,
        JsonElement Arguments
        );
}
