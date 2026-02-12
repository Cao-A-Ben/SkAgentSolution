using SKAgent.Core.Agent;
using SKAgent.Core.Protocols.MCP;

namespace SKAgent.Agents
{
    /// <summary>
    /// 【Agents 层 - MCP 协议 Agent】
    /// 通过 MCP（Model Context Protocol）协议调用外部系统/工具的 Agent。
    /// 对应 PlanStep.Agent = "mcp"，适用于外部工具调用场景。
    /// 当前使用 IMcpClient 的模拟实现（McpClient），后续可替换为真实的 HTTP/WebSocket/STDIO 实现。
    /// </summary>
    public class McpAgent : IAgent
    {
        /// <summary>MCP 客户端接口，负责与外部 MCP 服务通信。</summary>
        private readonly IMcpClient _client;

        /// <summary>Agent 名称，用于 RouterAgent 路由匹配。</summary>
        public string Name => "mcp";

        /// <summary>
        /// 初始化 MCP Agent。
        /// </summary>
        /// <param name="client">MCP 客户端实例。</param>
        public McpAgent(IMcpClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 执行 MCP 协议调用。将用户输入封装为 McpMessage 发送给 MCP 服务。
        /// </summary>
        /// <param name="context">当前步骤的执行上下文。</param>
        /// <returns>包含 MCP 服务响应的 AgentResult。</returns>
        public async Task<AgentResult> ExecuteAsync(AgentContext context)
        {
            // 1. 构建 McpMessage 并调用 MCP 客户端
            var result = await _client.CallAsync(new McpMessage(Name, context.Input), context.CancellationToken);

            // 2. 封装为 AgentResult 返回
            return new AgentResult
            {
                Output = result,
                IsSuccess = true
            };
        }
    }
}
