using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Planning
{
    /// <summary>
    /// 【Planning 层 - 执行计划模型】
    /// 表示 PlannerAgent 通过 LLM 生成的完整执行计划。
    /// 包含一个目标描述和一组有序的执行步骤。
    /// 由 PlannerAgent.CreatPlanAsync 从 LLM 返回的 JSON 反序列化而来，
    /// 随后通过 AgentRunContext.SetPlan 写入运行上下文，供 PlanExecutor 执行。
    /// </summary>
    public sealed class AgentPlan
    {
        /// <summary>
        /// 本次计划的目标描述，由 LLM 根据用户输入生成。
        /// 会被记录到 TurnRecord.Goal 中用于审计。
        /// </summary>
        public string Goal { get; init; } = string.Empty;

        /// <summary>
        /// 有序的执行步骤列表，每个步骤由一个 Agent 执行。
        /// PlanExecutor 按 Order 顺序依次执行。
        /// </summary>
        public IReadOnlyList<PlanStep> Steps { get; init; } = Array.Empty<PlanStep>();
    }
}
