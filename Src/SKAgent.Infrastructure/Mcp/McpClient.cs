using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SKAgent.Core.Protocols.MCP;

namespace SKAgent.Infrastructure.Mcp
{
    /// <summary>
    /// 【基础设施层 - MCP 客户端实现（模拟版）】
    /// IMcpClient 的模拟实现，当前为 stub，仅返回模拟响应。
    /// McpAgent 通过此客户端与外部 MCP 服务通信。
    /// 后续可替换为真实的 HTTP/WebSocket/STDIO 实现。
    /// </summary>
    public class McpClient : IMcpClient
    {
        /// <summary>
        /// 模拟调用 MCP 服务，延迟 100ms 后返回模拟响应。
        /// </summary>
        /// <param name="message">MCP 消息。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>模拟的响应字符串。</returns>
        public async Task<string> CallAsync(McpMessage message, CancellationToken cancellationToken = default)
        {
            // 模拟网络延迟
            await Task.Delay(100, cancellationToken);
            return $"[MCP:{message.Agent}] {message.Payload}";
        }
    }
}
