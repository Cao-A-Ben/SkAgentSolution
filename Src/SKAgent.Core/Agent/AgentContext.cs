using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Agent
{
    /// <summary>
    /// 【Core 抽象层 - Agent 执行上下文】
    /// 贯穿单次 Agent 调用的上下文对象，承载输入、路由目标、共享状态等信息。
    /// 
    /// 在 SSOT（Single Source of Truth）架构中，存在两个层级的 AgentContext：
    /// 1. Root 上下文 — 由 AgentRuntimeService 创建，保存用户原始输入，贯穿整个 Run 生命周期。
    /// 2. Step 上下文 — 由 PlanExecutor.CreateStepContext 为每个 PlanStep 独立创建，
    ///    继承会话级 ConversationState，但 Input 被替换为 PlanStep.Instruction，
    ///    保证步骤间互不污染。
    /// </summary>
    public sealed class AgentContext
    {
        /// <summary>
        /// 请求唯一标识 ID，同一次 Run 内所有 Step 共享相同的 RequestId。
        /// 用于日志追踪和调试关联。
        /// </summary>
        public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 当前 Agent 的输入内容。
        /// - Root 上下文中：为用户的原始输入文本。
        /// - Step 上下文中：为 PlanStep.Instruction（Planner 为该步骤生成的指令）。
        /// </summary>
        public string Input { get; set; } = string.Empty;

        /// <summary>
        /// 路由目标 Agent 名称，供 RouterAgent 使用。
        /// 由 PlanExecutor.CreateStepContext 从 PlanStep.Agent 写入，
        /// RouterAgent 据此在已注册的 IAgent 字典中查找并调用目标 Agent。
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// Planner 对该步骤的预期输出描述，用于后续反思（Reflection）或对齐验证。
        /// 来源于 PlanStep.ExpectedOutput，当前版本暂未启用反思机制，保留供后续扩展。
        /// </summary>
        public string? ExpectedOutput { get; set; } = string.Empty;

        /// <summary>
        /// 键值对状态字典，用于在 Agent 执行链中传递共享数据。
        /// Step 上下文创建时会从 AgentRunContext.ConversationState 复制数据，
        /// 常见的 key 包括:
        /// - "profile"        → Dictionary&lt;string, string&gt; 用户画像
        /// - "recent_turns"   → IReadOnlyList&lt;TurnRecord&gt; 最近对话记录
        /// - "persona"        → PersonaOptions 人格配置
        /// - "user_input"     → 用户原始输入（不随 Step 改变）
        /// - "conversation_id"→ 会话 ID
        /// </summary>
        public Dictionary<string, object> State { get; } = new();

        /// <summary>
        /// 取消令牌，用于支持请求中断。
        /// 从 HttpContext.RequestAborted 传入，贯穿整个异步调用链。
        /// </summary>
        public CancellationToken CancellationToken { get; init; }
    }
}
