using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Agent
{

    /// <summary>
    /// 统一输出
    /// </summary>
    public sealed class AgentResult
    {
        public string Output { get; init; } = string.Empty;

        public bool IsSuccess { get; init; } = true;

        public string? NextAgent { get; init; }
    }
}
