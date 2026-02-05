using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Execution
{
    public class StepExecutionResult
    {
        public int Order { get; init; }
        public string Agent { get; init; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
    }
}
