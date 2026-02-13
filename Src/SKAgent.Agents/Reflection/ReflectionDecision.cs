using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Reflection
{

    public enum ReflectionDecisionKind
    {
        None = 0,
        RetrySameStep = 1
    }
    public sealed record ReflectionDecision(
        ReflectionDecisionKind Action,
        string Reason
        );
}
