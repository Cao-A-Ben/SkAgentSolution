using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;
using SKAgent.Core.Agent;

namespace SKAgent.Agents
{
    //废弃 由Plan Executor取代
    [Obsolete("由Plan Executor取代")]
    public class OrchestratorAgent
    {
        private readonly PlannerAgent _planner;
        private readonly RouterAgent _router;

        public OrchestratorAgent(PlannerAgent planner, RouterAgent router)
        {
            _planner = planner;
            _router = router;
        }

        public async Task<AgentResult> ExecuteAsync(string input)
        {
            // 1. 让 LLM 决策
            var decision = await _planner.CreatPlanAsync(input);

            // 2. 构建上下文
            var context = new AgentContext
            {
                Input = input,
                //State = { ["target"] = decision.Agent }
            };

            // 3. 路由执行
            var result = await _router.ExecuteAsync(context);
            return result;

        }
    }
}
