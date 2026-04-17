using Microsoft.AspNetCore.Mvc;
using SkAgent.Runtime.Runtime;
using SKAgent.Core.Observability;
using SKAgent.Core.Replay;
using SKAgent.Host.Contracts;
using SKAgent.Infrastructure.Observability;
using SKAgent.Runtime.Observability;

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
        private readonly IRunEventLogFactory _runEventLogFactory;
        private readonly IReplayRunStore _replayRunStore;

        public AgentStreamController(
            AgentRuntimeService runtimeService,
            IRunEventLogFactory runEventLogFactory,
            IReplayRunStore replayRunStore)
        {
            _runtimeService = runtimeService;
            _runEventLogFactory = runEventLogFactory;
            _replayRunStore = replayRunStore;
        }

        /// <summary>
        /// 执行流式 Run。
        /// </summary>
        [HttpPost("run")]
        public async Task RunStream([FromBody] AgentRunRequest request)
        {

            var sseSink = new SseRunEventSink(Response);


            // 1. 解析或生成会话 ID
            var conversationId = !string.IsNullOrWhiteSpace(request.ConversationId)
                ? request.ConversationId : Guid.NewGuid().ToString("N");
            var runId = Guid.NewGuid().ToString("N");
            var eventLog = _runEventLogFactory.CreateAgentRunLog(runId);
            var startedAtUtc = DateTimeOffset.UtcNow;
            var sink = new CompositeRunEventSink(sseSink, eventLog.Sink);
            // 关键 让 runtime run 使用这个sink
            var run = await _runtimeService.RunAsync(
                conversationId,
                request.Input,
                request.PersonaName,
                runId,
                sink,
                HttpContext.RequestAborted);
            await _replayRunStore.SaveAsync(new ReplayRunRecord(
                RunId: run.RunId,
                Kind: "agent",
                ConversationId: run.ConversationId,
                Status: run.Status.ToString().ToLowerInvariant(),
                PersonaName: run.ConversationState.TryGetValue("personaName", out var personaName) ? personaName as string : null,
                Goal: string.IsNullOrWhiteSpace(run.Goal) ? null : run.Goal,
                InputPreview: Trim(run.UserInput, 240),
                FinalOutputPreview: Trim(run.FinalOutput, 240),
                StartedAtUtc: startedAtUtc,
                FinishedAtUtc: DateTimeOffset.UtcNow,
                EventLogPath: eventLog.Path),
                HttpContext.RequestAborted);


            // SSE 不需要 return body；RunAsync 结束即流结束
        }

        private static string? Trim(string? value, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Length <= maxChars ? value : value[..maxChars] + "...";
        }

    }
}
