using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Planning
{
    public sealed class PlanStep
    {
        /// <summary>
        /// 执行顺序
        /// </summary>
        /// <value>
        /// The order.
        /// </value>
        public int Order { get; init; }
        /// <summary>
        /// 目标Agent
        /// </summary>
        /// <value>
        /// The agent.
        /// </value>
        public string Agent { get; init; } = string.Empty;
        /// <summary>
        /// 给Agent的指令
        /// </summary>
        /// <value>
        /// The instruction.
        /// </value>
        public string Instruction { get; init; } = string.Empty;
        /// <summary>
        /// Planner对于该步骤的预期输出  用于反思
        /// </summary>
        /// <value>
        /// The expected output.
        /// </value>
        public string? ExpectedOutput { get; init; }
    }
}
