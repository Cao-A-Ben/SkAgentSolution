using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Planning
{
    /// <summary>
    /// 【Planning 层 - 计划步骤模型】
    /// 表示执行计划中的单个步骤，指定由哪个 Agent 执行什么指令。
    /// 由 PlannerAgent 通过 LLM 生成，嵌套在 AgentPlan.Steps 中。
    /// PlanExecutor 为每个 PlanStep 创建独立的 StepContext 执行。
    /// </summary>
    public sealed class PlanStep
    {
        /// <summary>
        /// 执行顺序编号，PlanExecutor 按此字段升序执行。
        /// </summary>
        public int Order { get; init; }

        /// <summary>
        /// 步骤类型
        /// </summary>
        public PlanStepKind Kind { get; init; } = PlanStepKind.Agent;

        /// <summary>
        /// 当 Kind=Agent: 目标Agent 名称如（"chat"、"mcp"）
        /// 当 Kind=Tool: 目标工具名称如 ("SearchTool","http.get","string.upper")
        /// </summary>
        public string Target { get; init; } = string.Empty;

        ///// <summary>
        ///// 目标 Agent 名称，对应 IAgent.Name。
        ///// PlanExecutor 将此值写入 StepContext.Target，由 RouterAgent 进行路由匹配。
        ///// 常见值: "chat"、"mcp"。
        ///// </summary>
        //public string Agent { get; init; } = string.Empty;

        /// <summary>
        /// Planner 为该步骤生成的具体指令，
        /// 当Kind=Agent 作为 StepContext.Input 传递给目标 Agent。
        /// 当Kind=Tool 可为空，工具调用不一定需要
        /// 例如: "根据用户的作息和地点，给出养生建议"。
        /// </summary>
        public string? Instruction { get; init; }


        /// <summary>
        /// 当Kind=Agent 通常为空
        ///当 Kind=Tool  Json字符串， （例如:{"text":"abc"}, Planner生成更方便）
        /// Executor 执行时再parse 成 JsonDocument/JsonElement 
        /// </summary>
        public string? ArgumentsJson { get; init; }

        /// <summary>
        /// Planner 对该步骤的预期输出描述，用于后续反思机制的对齐验证。
        /// 当前版本为预留字段，暂未启用反思逻辑。
        /// </summary>
        public string? ExpectedOutput { get; init; }
    }
}
