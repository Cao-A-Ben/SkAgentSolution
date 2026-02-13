using Microsoft.AspNetCore.Mvc;
using SKAgent.Agents.Observability.Exporters;
using SKAgent.Agents.Runtime;
using SKAgent.Host.Contracts;

namespace SKAgent.Host.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AgentStreamController : ControllerBase
    {

        private readonly AgentRuntimeService _runtimeService;

        public AgentStreamController(AgentRuntimeService runtimeService)
        {
            _runtimeService = runtimeService;
        }

        [HttpPost("run")]
        public async Task RunStream([FromBody] AgentRunRequest request)
        {

            var sink = new SseRunEventSink(Response);


            // 1. 解析或生成会话 ID
            var conversationId = !string.IsNullOrWhiteSpace(request.ConversationId)
                ? request.ConversationId : Guid.NewGuid().ToString("N");
            // 关键 让 runtime run 使用这个sink
            await _runtimeService.RunAsync(conversationId, request.Input, eventSink: sink, ct: HttpContext.RequestAborted);


            // SSE 不需要 return body；RunAsync 结束即流结束
        }

    }
}
