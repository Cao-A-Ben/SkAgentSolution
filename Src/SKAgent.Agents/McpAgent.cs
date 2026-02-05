using SKAgent.Core.Agent;
using SKAgent.Core.Protocols.MCP;

namespace SKAgent.Agents
{
    public class McpAgent : IAgent
    {
        private readonly IMcpClient _client;

        public string Name => "mcp";

        public McpAgent(IMcpClient client)
        {
            _client = client;
        }

        public async Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            var result = await _client.CallAsync(new McpMessage(Name, context.Input), context.CancellationToken);

            return new AgentResult
            {
                Output = result,
                IsSuccess = true
            };
        }
    }
}
