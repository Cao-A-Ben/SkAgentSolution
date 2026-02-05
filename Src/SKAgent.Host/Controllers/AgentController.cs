using Microsoft.AspNetCore.Mvc;
using SKAgent.Agents;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;
using SKAgent.Core.Agent;

namespace SKAgent.Host.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgentController : ControllerBase
    {
        private readonly PlannerAgent _planner;
        private readonly PlanExecutor _executor;

        public AgentController(PlannerAgent planner, PlanExecutor executor)
        {
            _planner = planner;
            _executor = executor;
        }


        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] string input)
        {
            // 1. 生成Plan
            var plan = await _planner.CreatPlanAsync(input);

            var agentContext = new AgentContext
            {
                Input = input,
                // 可选: 传递预期输出给Planner
                ExpectedOutput = string.Empty,
                CancellationToken = HttpContext.RequestAborted
            };

            var run = new AgentRunContext(agentContext, plan.Goal, plan!);
            // 2. 执行Plan
            await _executor.ExecuteAsync(run);

            return Ok(new
            {
                runId = run.RunId,
                goal = run.Goal,
                status = run.Status.ToString(),
                output = run.FinalOutput,
                steps = run.Steps.Select(s => new {
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
