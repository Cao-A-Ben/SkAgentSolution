using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Agent
{
    public sealed class AgentContext
    {

        public string RequestId { get; init; } = Guid.NewGuid().ToString("N");
        /// <summary>
        /// 用户/上游给当前系统的输入
        /// </summary>
        /// <value>
        /// The input.
        /// </value>
        public string Input { get; set; } = string.Empty;
        /// <summary>
        /// 路由目标 Agent 名称 给router使用
        /// </summary>
        /// <value>
        /// The target.
        /// </value>
        public string? Target { get; set; }

        /// <summary>
        /// Planner对齐/反思的预期输出
        /// </summary>
        /// <value>
        /// The expected output.
        /// </value>
        public string? ExpectedOutput { get; set; } = string.Empty;
        /// <summary>
        /// Gets the state.
        /// </summary>
        /// <value>
        /// The state.
        /// </value>
        public Dictionary<string, object> State { get; } = new();
        /// <summary>
        /// Gets the cancellation token.
        /// </summary>
        /// <value>
        /// The cancellation token.
        /// </value>
        public CancellationToken CancellationToken { get; init; }
    }
}
