using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具执行结果】
    /// 封装一次工具调用的完整结果，包含成功标志、输出、错误和性能指标。
    /// 由 ITool.InvokeAsync 返回，ToolInvoker 补充 Metrics 后传递给 PlanExecutor。
    /// </summary>
    /// <param name="Success">是否执行成功。</param>
    /// <param name="Output">工具输出的 JSON 数据。</param>
    /// <param name="Error">失败时的错误信息（可选）。</param>
    /// <param name="Metrics">执行性能指标（可选），由 ToolInvoker 自动填充。</param>
    public sealed record ToolResult(
        bool Success,
        JsonElement Output,
        ToolError? Error = null,
        ToolMetrics? Metrics = null);


    /// <summary>
    /// 【Tools 抽象层 - 工具性能指标】
    /// 记录工具执行的耗时，由 ToolInvoker 通过 Stopwatch 测量并写入 ToolResult.Metrics。
    /// </summary>
    /// <param name="LatencyMs">执行耗时（毫秒）。</param>
    public sealed record ToolMetrics(
       long LatencyMs
        );
}
