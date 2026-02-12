using Microsoft.AspNetCore.Mvc;
using SKAgent.Agents;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;
using SKAgent.Core.Agent;
using SKAgent.Host.Contracts;

namespace SKAgent.Host.Controllers
{
    /// <summary>
    /// 【Host 层 - Agent API 控制器】
    /// 提供 HTTP API 端点，是客户端与 Agent 系统交互的唯一入口。
    /// 接收用户请求并委托给 AgentRuntimeService 执行完整的 Agent 运行流程。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AgentController : ControllerBase
    {
        /// <summary>Agent 运行时服务，串联所有环节的总调度器。</summary>
        private readonly AgentRuntimeService _runtimeService;

        /// <summary>
        /// 初始化控制器。
        /// </summary>
        /// <param name="runtimeService">DI 注入的 AgentRuntimeService 实例。</param>
        public AgentController(AgentRuntimeService runtimeService)
        {
            _runtimeService = runtimeService;
        }

        /// <summary>
        /// 【已废弃】早期版本的 GET 接口，使用字符串输入和匿名对象返回。
        /// 已被 POST /api/agent/run（DTO 版本）替代。
        /// </summary>
        [Obsolete]
        [HttpGet("run-obs")]
        public async Task<IActionResult> Run([FromBody] string input)
        {
            var ct = HttpContext.RequestAborted;

            // 从请求头读取会话 ID，若未提供则自动生成
            var conversationId = Request.Headers.TryGetValue("X-Conversation-Id", out var v)
                && !string.IsNullOrWhiteSpace(v)
                ? v.ToString() : Guid.NewGuid().ToString("N");

            // 调用 AgentRuntimeService 执行完整流程
            var run = await _runtimeService.RunAsync(conversationId, input, ct);

            // 返回匿名对象（早期版本格式）
            return Ok(new
            {
                conversationId,
                runId = run.RunId,
                goal = run.Goal,
                status = run.Status.ToString(),
                output = run.FinalOutput,
                steps = run.Steps.Select(s => new
                {
                    order = s.Order,
                    agent = s.Agent,
                    status = s.Status.ToString(),
                    output = s.Output,
                    error = s.Error
                })
            });
        }

        /// <summary>
        /// 【主接口】执行 Agent 运行流程。
        /// 客户端发送 { conversationId?, input } → 服务端返回 { conversationId, runId, output, steps, profileSnapshot }。
        /// 
        /// 完整流程：
        /// 1. 解析 conversationId（若未提供则自动生成）。
        /// 2. 调用 AgentRuntimeService.RunAsync 执行完整的 Agent 运行流程。
        /// 3. 从运行结果中提取 profileSnapshot。
        /// 4. 映射为 AgentRunResponse DTO 返回。
        /// </summary>
        /// <param name="req">请求 DTO，包含 conversationId 和 input。</param>
        /// <returns>包含完整运行结果的 AgentRunResponse。</returns>
        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] AgentRunRequest req)
        {
            var ct = HttpContext.RequestAborted;

            // 1. 解析或生成会话 ID
            var conversationId = !string.IsNullOrWhiteSpace(req.ConversationId)
                ? req.ConversationId : Guid.NewGuid().ToString("N");

            // 2. 调用运行时服务执行完整流程
            var run = await _runtimeService.RunAsync(conversationId, req.Input, ct);

            // 3. 从 ConversationState 中提取画像快照
            var profileSnapshot = run.ConversationState.TryGetValue("profile", out var p) ? p as Dictionary<string, string> : null;

            // 4. 映射为强类型 DTO 返回
            return Ok(new AgentRunResponse
            {
                ConversationId = conversationId,
                RunId = run.RunId,
                Goal = run.Goal,
                Status = run.Status.ToString(),
                Output = run.FinalOutput ?? "",
                ProfileSnapshot = profileSnapshot,
                Steps = [.. run.Steps.Select(s => new AgentStepResponse
                {
                    Order = s.Order,
                    Agent = s.Agent,
                    Status = s.Status.ToString(),
                    Output = s.Output ?? "",
                    Error = s.Error
                })]
            });
        }
    }
}
