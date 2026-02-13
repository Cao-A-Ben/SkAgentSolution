using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Execution;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Planning;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Runtime
{
    /// <summary>
    /// 【Runtime 层 - 运行上下文（SSOT 核心对象）】
    /// 单次用户请求的完整运行上下文，是整个 Runtime 流程的"单一真实数据源"（SSOT）。
    /// 
    /// 生命周期：
    /// 由 AgentRuntimeService.RunAsync 创建 → 经过 Planner/Executor 各阶段 → 最终返回给 Controller。
    /// 
    /// 包含：
    /// - Root AgentContext（原始请求上下文）
    /// - 会话级 ConversationState（profile/memory/persona 等共享数据）
    /// - 计划（AgentPlan）和步骤执行记录
    /// - 最终输出和运行状态
    /// </summary>
    public sealed class AgentRunContext
    {
        /// <summary>
        /// 本次运行的唯一标识 ID，用于追踪和审计。
        /// </summary>
        public string RunId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Root 上下文，与 Router/Agents 统一的顶层上下文对象。
        /// 包含用户原始输入和初始 State。
        /// 注意：执行过程中 Root.State 可能被 SyncStateBackToRoot 更新。
        /// </summary>
        public AgentContext Root { get; set; }

        /// <summary>
        /// 本次运行的目标描述，来自 PlannerAgent 生成的 AgentPlan.Goal。
        /// 通过 SetPlan 方法设置。
        /// </summary>
        public string Goal { get; private set; } = string.Empty;

        /// <summary>
        /// PlannerAgent 生成的当前执行计划，包含有序的步骤列表。
        /// 通过 SetPlan 方法设置，PlanExecutor 据此逐步执行。
        /// </summary>
        public AgentPlan? Plan { get; set; }

        /// <summary>
        /// 会话唯一标识 ID，用于关联短期记忆和用户画像。
        /// 来自客户端请求或自动生成。
        /// </summary>
        public string ConversationId { get; }

        /// <summary>
        /// 最近的对话回合记录，由 AgentRuntimeService 从 IShortTermMemory 加载。
        /// 供 PlannerAgent 和 ChatContextComposer 参考上下文。
        /// </summary>
        public IReadOnlyList<TurnRecord> RecentTurns { get; private set; } = Array.Empty<TurnRecord>();

        /// <summary>
        /// 所有步骤的执行跟踪列表，由 PlanExecutor 逐步写入。
        /// 最终映射为 AgentRunResponse.Steps 返回给客户端。
        /// </summary>
        public IList<PlanStepExecution> Steps { get; } = new List<PlanStepExecution>();

        /// <summary>
        /// 当前运行的生命周期状态（Initialized → Executing → Completed/Failed）。
        /// </summary>
        public AgentRunStatus Status { get; set; } = AgentRunStatus.Initialized;

        /// <summary>
        /// 最终聚合输出文本，由 PlanExecutor 拼接所有步骤的 Output 而成。
        /// 映射为 AgentRunResponse.Output。
        /// </summary>
        public string? FinalOutput { get; set; }

        /// <summary>
        /// 用户原始输入的不可变副本。
        /// 无论 Step 如何修改 Root.Input，此值始终保留最初的用户输入。
        /// 用于记忆写入、画像提取和审计。
        /// </summary>
        public string UserInput { get; set; }

        /// <summary>
        /// 会话级共享状态字典，是 Step 之间传递数据的桥梁。
        /// PlanExecutor 在创建 StepContext 时从此字典复制数据，
        /// 步骤执行完毕后再将 StepContext.State 合并回来。
        /// 
        /// 常见 key：
        /// - "recent_turns" → 最近对话记录
        /// - "profile"      → 用户画像字典
        /// - "persona"      → 人格配置
        /// </summary>
        public Dictionary<string, object> ConversationState { get; } = new(StringComparer.OrdinalIgnoreCase);


        /// <summary>
        /// 本次运行中所有 Kind=Tool 步骤的调用记录，由 PlanExecutor 逐步写入。
        /// 用于审计、调试和后续反思机制。
        /// </summary>
        public List<ToolCallRecord> ToolCalls { get; } = new();

        /// <summary>
        /// 初始化运行上下文。
        /// </summary>
        /// <param name="context">Root AgentContext，包含用户原始输入。</param>
        /// <param name="conversationId">会话唯一标识 ID。</param>
        public AgentRunContext(AgentContext context, string conversationId)
        {
            Root = context ?? throw new ArgumentNullException(nameof(context));
            ConversationId = conversationId;
            UserInput = context.Input;

            // 将 Root.State 中的初始数据复制到会话级 ConversationState
            foreach (var kv in context.State)
            {
                ConversationState[kv.Key] = kv.Value;
            }
        }

        /// <summary>
        /// 设置执行计划和目标。由 AgentRuntimeService 在 PlannerAgent 返回计划后调用。
        /// </summary>
        /// <param name="plan">PlannerAgent 生成的执行计划。</param>
        public void SetPlan(AgentPlan plan)
        {
            Plan = plan;
            Goal = plan.Goal;
        }

        /// <summary>
        /// 设置最近的对话回合记录。由 AgentRuntimeService 从 IShortTermMemory 加载后调用。
        /// </summary>
        /// <param name="turns">最近的回合记录列表。</param>
        public void SetRecentTurns(IReadOnlyList<TurnRecord> turns)
        {
            RecentTurns = turns ?? Array.Empty<TurnRecord>();
        }

        /// <summary>
        /// 将会话级 ConversationState 同步回 Root.State。
        /// 由 PlanExecutor 在计划执行完毕（或失败）后调用，
        /// 确保外部可通过 Root.State 查看最终的共享状态。
        /// </summary>
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
