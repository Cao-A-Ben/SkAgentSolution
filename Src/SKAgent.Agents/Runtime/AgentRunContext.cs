using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Planning;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Runtime
{
    public sealed class AgentRunContext
    {
        public string RunId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 关键: 与Router/Agents 统一的上下文 同一个State会贯穿整个Run
        /// </summary>
        public AgentContext Root { get; set; }

        //目标 来自Planner
        public string Goal { get; private set; } = string.Empty;
        //当前计划
        public AgentPlan? Plan { get; set; }
        //会话ID 
        public string ConversationId { get; }

        public IReadOnlyList<TurnRecord> RecentTurns { get; private set; } = Array.Empty<TurnRecord>();

        public IList<PlanStepExecution> Steps { get; } = new List<PlanStepExecution>();

        public AgentRunStatus Status { get; set; } = AgentRunStatus.Initialized;
        ////最终聚合输出
        public string? FinalOutput { get; set; }

        //永远不变，保留用户原始输入，以防Step覆盖Root.Input，用于记忆、画像、审计
        public string UserInput { get; set; }

        // ✅ 会话级共享 State（不要让 Step 覆盖 Root）
        public Dictionary<string, object> ConversationState { get; } = new(StringComparer.OrdinalIgnoreCase);




        public AgentRunContext(AgentContext context, string conversationId)
        {
            Root = context ?? throw new ArgumentNullException(nameof(context));
            //Goal = plan.Goal ?? string.Empty;
            //Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            ConversationId = conversationId;
            UserInput = context.Input;
            //初始化 会话 state 可以放一些默认值，或者在外部设置
            foreach (var kv in context.State)
            {
                ConversationState[kv.Key] = kv.Value;
            }
        }

        public void SetPlan(AgentPlan plan)
        {
            Plan = plan;
            Goal = plan.Goal;
        }

        public void SetRecentTurns(IReadOnlyList<TurnRecord> turns)
        {
            RecentTurns = turns ?? Array.Empty<TurnRecord>();
        }

        public void SyncStateBackToRoot()
        {
            Root.State.Clear();
            foreach (var kv in ConversationState)
            {
                Root.State[kv.Key] = kv.Value;
            }
        }
    }
}
