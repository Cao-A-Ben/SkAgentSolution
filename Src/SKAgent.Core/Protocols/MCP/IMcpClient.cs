using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.MCP
{
    public interface IMcpClient
    {
        Task<string> CallAsync(McpMessage message, CancellationToken cancellationToken = default);
    }
}
