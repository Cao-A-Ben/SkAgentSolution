using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;

namespace SKAgent.Agents.Runtime
{
    public sealed class PlanStepExecution
    {
        public required PlanStep Step { get; set; }

        public StepExecutionStatus Status { get; set; } = StepExecutionStatus.Pending;
        public string? Output { get; set; }

        public string? Error { get; set; }

        //可选：给反思或调试用
        public string Agent => Step.Agent;
        public string Instruction => Step.Instruction;
        public int Order => Step.Order;

    }
}
