using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SKAgent.Core.Protocols.MCP;

namespace SKAgent.Infrastructure.Mcp
{
    public class McpClient : IMcpClient
    {
        public async Task<string> CallAsync(McpMessage message, CancellationToken cancellationToken = default)
        {
            // Http /WebSocket / STDIO
            await Task.Delay(100, cancellationToken);
            return $"[MCP:{message.Agent}] {message.Payload}";
        }
    }
}
