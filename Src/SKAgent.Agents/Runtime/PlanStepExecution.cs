using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;

namespace SKAgent.Agents.Runtime
{
    /// <summary>
    /// 【Runtime 层 - 计划步骤执行跟踪对象】
    /// 封装单个 PlanStep 的执行状态、输出和错误信息。
    /// 由 PlanExecutor 在执行每个步骤时创建并写入 AgentRunContext.Steps 列表。
    /// 最终通过 AgentController 映射为 AgentStepResponse 返回给客户端。
    /// </summary>
    public sealed class PlanStepExecution
    {
        /// <summary>
        /// 对应的计划步骤定义（包含 Agent、Instruction、Order 等）。
        /// </summary>
        public required PlanStep Step { get; set; }

        /// <summary>
        /// 当前步骤的执行状态，由 PlanExecutor 在执行前后更新。
        /// </summary>
        public StepExecutionStatus Status { get; set; } = StepExecutionStatus.Pending;

        /// <summary>
        /// Agent 执行后的输出文本，对应 AgentResult.Output。
        /// </summary>
        public string? Output { get; set; }

        /// <summary>
        /// 执行失败时的错误信息（异常消息或失败原因）。
        /// </summary>
        public string? Error { get; set; }

        /// <summary>便捷属性：目标 Agent 名称，等价于 Step.Target，用于反思或调试。</summary>
        public string Agent => Step.Target;

        /// <summary>便捷属性：步骤类型字符串，等价于 Step.Kind.ToString()。</summary>
        public string Kind => Step.Kind.ToString();

        /// <summary>便捷属性：带前缀的目标显示名，如 "tool:string.upper" 或 "agent:chat"。</summary>
        public string DisplayTarget =>
    Step.Kind == PlanStepKind.Tool ? $"tool:{Step.Target}" : $"agent:{Step.Target}";

        /// <summary>便捷属性：步骤指令，等价于 Step.Instruction。</summary>
        public string? Instruction => Step.Instruction;

        /// <summary>便捷属性：执行顺序，等价于 Step.Order。</summary>
        public int Order => Step.Order;
    }
}
