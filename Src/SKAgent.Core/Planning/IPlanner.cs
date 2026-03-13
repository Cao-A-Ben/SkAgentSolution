using System;
using System.Collections.Generic;
using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Core.Memory.ShortTerm;

namespace SKAgent.Core.Planning
{
    public interface IPlanner
    {

        Task<AgentPlan> CreatePlanAsync(PlanRequest request);

        /// <summary>
        /// Planner 所需的最小输入（纯模型，不能引用 Application 类型）
        /// </summary>
        public sealed record PlanRequest(
            string RunId,
            string ConversationId,
            string UserInput,
            IReadOnlyList<TurnRecord> RecentTurns,
            IReadOnlyDictionary<string, string> Profile,
            string PlannerHint,
            bool DebugPlan = false
        );
    }
}
