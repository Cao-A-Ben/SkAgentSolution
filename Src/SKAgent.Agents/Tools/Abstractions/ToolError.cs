using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Tools.Abstractions
{
    /// <summary>
    /// 【Tools 抽象层 - 工具错误模型】
    /// 工具执行失败时的错误信息，嵌套在 ToolResult.Error 中。
    /// 由 ToolInvoker 在异常/超时/业务失败时构建。
    /// </summary>
    /// <param name="Code">错误码，如 "tool_not_found"、"tool_timeout"、"http_error"。</param>
    /// <param name="Message">错误描述文本。</param>
    /// <param name="Details">额外错误详情（可选），如异常类型全名。</param>
    public sealed record ToolError(
        string Code,
        string Message,
        object? Details = null
        );
}
