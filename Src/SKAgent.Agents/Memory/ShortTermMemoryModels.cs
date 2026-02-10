using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Memory
{
    public sealed class TurnRecord
    {
        public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;

        public string UserInput { get; init; } = string.Empty;

        public string AssistantOutput { get; init; } = string.Empty;

        public string Goal { get; init; } = string.Empty;

        public IReadOnlyList<StepRecord> Steps { get; init; } = Array.Empty<StepRecord>();
    }

    public sealed class StepRecord
    {
        public int Order { get; init; }
        public string Agent { get; init; } = string.Empty;
        public string Instruction { get; init; } = string.Empty;
        public string Output { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }
}
