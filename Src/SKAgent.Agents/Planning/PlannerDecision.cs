using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Planning
{
    /// <summary>
    /// 【Planning 层 - Planner 决策模型（早期版本 / 已废弃）】
    /// 早期 Week2 中用于表示 Planner 的单步决策结果（选择哪个 Agent）。
    /// 已被 Week3+ 的 AgentPlan（多步计划）替代，保留仅供参考。
    /// </summary>
    public sealed class PlannerDecision
    {
        /// <summary>
        /// 被选中的目标 Agent 名称。
        /// </summary>
        public string Agent { get; set; } = string.Empty;

        /// <summary>
        /// Planner 选择该 Agent 的理由说明。
        /// </summary>
        public string? Reason { get; set; }
    }
}
