using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;
using SKAgent.Core.Agent;

namespace SKAgent.Agents
{
    /// <summary>
    /// 【Agents 层 - 编排 Agent（已废弃）】
    /// Week2/Week3 早期版本中的编排 Agent，负责协调 Planner 和 Router。
    /// 在 Week3+ 架构中已被 AgentRuntimeService + PlanExecutor 组合替代。
    /// 保留仅供参考和向后兼容。
    /// </summary>
    [Obsolete("由 AgentRuntimeService + PlanExecutor 取代")]
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
            //var decision = await _planner.CreatPlanAsync(input);

            //// 2. 构建上下文
            //var context = new AgentContext
            //{
            //    Input = input,
            //    //State = { ["target"] = decision.Agent }
            //};

            //// 3. 路由执行
            //var result = await _router.ExecuteAsync(context);
            //return result;

            return default;

        }
    }
}
