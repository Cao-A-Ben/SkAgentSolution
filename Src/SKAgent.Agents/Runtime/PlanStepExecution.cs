using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;

namespace SKAgent.Agents.Runtime
{
    public sealed class PlanStepExecution
    {
        public required PlanStep Step { get; set; }

        public string? Output { get; set; }

        public string? Error { get; set; }

        public StepExecutionStatus Status { get; set; } = StepExecutionStatus.Pending;
    }
}
