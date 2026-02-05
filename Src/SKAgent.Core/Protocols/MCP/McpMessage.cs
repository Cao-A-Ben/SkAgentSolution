using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.MCP
{
    public sealed record McpMessage(string Agent, string Payload);
}
