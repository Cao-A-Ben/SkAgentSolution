using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Reflection
{
    /// <summary>
    /// 反思决策的动作类型。
    /// </summary>
    public enum ReflectionDecisionKind
    {
        None = 0,
        RetrySameStep = 1
    }

    /// <summary>
    /// 反思决策结果，包含动作与原因。
    /// </summary>
    public sealed record ReflectionDecision(
        ReflectionDecisionKind Action,
        string Reason
        );
}
