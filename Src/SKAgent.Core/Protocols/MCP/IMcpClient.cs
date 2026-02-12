using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.MCP
{
    /// <summary>
    /// 【Core 协议层 - MCP（Model Context Protocol）客户端接口】
    /// 定义调用外部系统/工具/协议的抽象契约。
    /// McpAgent 通过此接口与外部 MCP 服务通信。
    /// 基础设施层通过 McpClient 提供模拟实现（当前为 stub，后续可替换为真实 HTTP/WebSocket/STDIO 调用）。
    /// </summary>
    public interface IMcpClient
    {
        /// <summary>
        /// 异步调用 MCP 服务并获取响应。
        /// </summary>
        /// <param name="message">MCP 消息，包含目标 Agent 名称和 Payload。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>MCP 服务的响应文本。</returns>
        Task<string> CallAsync(McpMessage message, CancellationToken cancellationToken = default);
    }
}
