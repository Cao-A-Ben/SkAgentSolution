using Microsoft.AspNetCore.Mvc;
using SKAgent.Agents;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Planning;
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

            var executionResult = await _executor.ExecuteAsync(plan);


            return Ok(executionResult);

        }
    }
}
