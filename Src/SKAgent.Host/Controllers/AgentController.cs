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
    [ApiController]
    [Route("api/[controller]")]
    public class AgentController : ControllerBase
    {
        private readonly AgentRuntimeService _runtimeService;

        public AgentController(AgentRuntimeService runtimeService)
        {
            _runtimeService = runtimeService;
        }

        [Obsolete]
        [HttpGet("run-obs")]
        public async Task<IActionResult> Run([FromBody] string input)
        {
            var ct = HttpContext.RequestAborted;

            var conversationId = Request.Headers.TryGetValue("X-Conversation-Id", out var v)
                && !string.IsNullOrWhiteSpace(v)
                ? v.ToString() : Guid.NewGuid().ToString("N");

            var run = await _runtimeService.RunAsync(conversationId, input, ct);

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


        [HttpPost("run")]

        public async Task<IActionResult> Run([FromBody] AgentRunRequest req)
        {
            var ct = HttpContext.RequestAborted;
            var conversationId = !string.IsNullOrWhiteSpace(req.ConversationId)
                ? req.ConversationId : Guid.NewGuid().ToString("N");

            var run = await _runtimeService.RunAsync(conversationId, req.Input, ct);
            var profileSnapshot = run.ConversationState.TryGetValue("profile", out var p)? p as Dictionary<string, string>: null;

            return Ok(new AgentRunResponse
            {
                ConversationId = conversationId,
                RunId = run.RunId,
                Goal = run.Goal,
                Status = run.Status.ToString(),
                Output = run.FinalOutput??"",
                ProfileSnapshot = profileSnapshot,
                Steps = [.. run.Steps.Select(s => new AgentStepResponse
                {
                    Order = s.Order,
                    Agent = s.Agent,
                    Status = s.Status.ToString(),
                    Output = s.Output??"",
                    Error = s.Error
                })]
            });
        }


    }
}
