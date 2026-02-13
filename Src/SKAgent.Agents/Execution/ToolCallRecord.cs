using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Execution
{
    /// <summary>
    /// 【Execution 层 - 工具调用记录】
    /// 记录单次工具调用的完整信息，由 PlanExecutor 在执行 Kind=Tool 步骤后构建。
    /// 写入 AgentRunContext.ToolCalls 列表，用于审计、调试和后续反思机制。
    /// </summary>
    /// <param name="StepOrder">所属步骤的执行顺序，对应 PlanStep.Order。</param>
    /// <param name="ToolName">工具名称，对应 PlanStep.Target。</param>
    /// <param name="ArgsPreview">调用参数预览（截断版），避免大 JSON 占用过多空间。</param>
    /// <param name="Success">是否执行成功。</param>
    /// <param name="OutputPreview">输出预览（截断版）。</param>
    /// <param name="ErrorCode">错误码（失败时）。</param>
    /// <param name="ErrorMessage">错误描述（失败时）。</param>
    /// <param name="LatencyMs">执行耗时毫秒数。</param>
    public sealed record ToolCallRecord(
        int StepOrder,
        string ToolName,
        string? ArgsPreview,
        bool Success,
        string? OutputPreview,
        string? ErrorCode,
        string? ErrorMessage,
        long LatencyMs
        );
}
