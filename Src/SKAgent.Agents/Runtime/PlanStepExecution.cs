using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Runtime
{
    public sealed class PlanStepExecution
    {
        public int Order { get; init; }
        public string Agent { get; init; } = string.Empty;

        public string Input { get; set; } = string.Empty;

        public string? Output { get; set; }
    }
}
