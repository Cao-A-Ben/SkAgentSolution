using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Execution
{
    public sealed class PlanExecutionResult
    {
        public string Goal { get; init; } = string.Empty;

        public IReadOnlyList<StepExecutionResult> Steps { get; init; } = Array.Empty<StepExecutionResult>();
    }
}
