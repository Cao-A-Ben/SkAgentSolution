using Microsoft.AspNetCore.Mvc;
using SkAgent.Runtime.Runtime;
using SKAgent.Host.Contracts;
using SKAgent.Infrastructure.Observability;

namespace SKAgent.Host.Controllers
{

    /// <summary>
    /// 流式运行控制器（SSE）。
    /// 将运行时事件以 `text/event-stream` 形式实时推送给客户端。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AgentStreamController : ControllerBase
    {

        private readonly AgentRuntimeService _runtimeService;

        public AgentStreamController(AgentRuntimeService runtimeService)
        {
            _runtimeService = runtimeService;
        }

        /// <summary>
        /// 执行流式 Run。
        /// </summary>
        [HttpPost("run")]
        public async Task RunStream([FromBody] AgentRunRequest request)
        {

            var sink = new SseRunEventSink(Response);


            // 1. 解析或生成会话 ID
            var conversationId = !string.IsNullOrWhiteSpace(request.ConversationId)
                ? request.ConversationId : Guid.NewGuid().ToString("N");
            // 关键 让 runtime run 使用这个sink
            await _runtimeService.RunAsync(conversationId, request.Input, request.PersonaName, eventSink: sink, ct: HttpContext.RequestAborted);


            // SSE 不需要 return body；RunAsync 结束即流结束
        }

    }
}
