using Microsoft.AspNetCore.Mvc;
using SKAgent.Agents;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;
using SKAgent.Core.Agent;

namespace SKAgent.Host.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgentController : ControllerBase
    {
        private readonly AgentRuntimeService _runtimeService;

        public AgentController( AgentRuntimeService runtimeService)
        {
            _runtimeService = runtimeService;
        }


        [HttpPost("run")]
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




    }
}
