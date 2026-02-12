using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.MCP
{
    /// <summary>
    /// 【Core 协议层 - MCP 消息记录】
    /// Model Context Protocol 的消息载体，使用 record 类型保证不可变性。
    /// 由 McpAgent 构建并传递给 IMcpClient.CallAsync 执行。
    /// </summary>
    /// <param name="Agent">发起调用的 Agent 名称。</param>
    /// <param name="Payload">调用的请求内容（通常为用户输入或指令文本）。</param>
    public sealed record McpMessage(string Agent, string Payload);
}
