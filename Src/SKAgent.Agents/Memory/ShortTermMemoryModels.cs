using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Memory
{
    /// <summary>
    /// 【Memory 层 - 回合记录模型】
    /// 表示一次完整的对话回合，包含用户输入、助手输出、目标以及执行步骤明细。
    /// 由 AgentRuntimeService.CommitShortTermMemoryAsync 在回合结束后构建并写入 IShortTermMemory。
    /// 在下次请求时通过 GetRecentAsync 加载，供 PlannerAgent 和 ChatContextComposer 参考上下文。
    /// </summary>
    public sealed class TurnRecord
    {
        /// <summary>
        /// 回合发生的 UTC 时间戳，用于排序和调试。
        /// </summary>
        public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 用户的原始输入文本，对应 AgentRunContext.UserInput。
        /// </summary>
        public string UserInput { get; init; } = string.Empty;

        /// <summary>
        /// 助手的最终输出文本，对应 AgentRunContext.FinalOutput。
        /// </summary>
        public string AssistantOutput { get; init; } = string.Empty;

        /// <summary>
        /// 本次回合的目标描述，来自 PlannerAgent 生成的 AgentPlan.Goal。
        /// </summary>
        public string Goal { get; init; } = string.Empty;

        /// <summary>
        /// 本次回合的执行步骤明细，记录每个 Plan Step 的执行情况。
        /// </summary>
        public IReadOnlyList<StepRecord> Steps { get; init; } = Array.Empty<StepRecord>();
    }

    /// <summary>
    /// 【Memory 层 - 步骤记录模型】
    /// 表示单个执行步骤的记录，嵌套在 TurnRecord 中。
    /// 用于审计、调试和后续的反思机制。
    /// </summary>
    public sealed class StepRecord
    {
        /// <summary>
        /// 步骤执行顺序，对应 PlanStep.Order。
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// 执行该步骤的 Agent 名称，对应 PlanStep.Agent。
        /// </summary>
        public string Agent { get; init; } = string.Empty;

        /// <summary>
        /// Planner 为该步骤生成的指令文本，对应 PlanStep.Instruction。
        /// </summary>
        public string Instruction { get; init; } = string.Empty;

        /// <summary>
        /// 该步骤的实际输出文本。
        /// </summary>
        public string Output { get; init; } = string.Empty;

        /// <summary>
        /// 该步骤的执行状态字符串，对应 StepExecutionStatus 的名称。
        /// </summary>
        public string Status { get; init; } = string.Empty;
    }
}
