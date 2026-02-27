namespace SKAgent.Core.Memory.ShortTerm;

/// <summary>
/// 一次对话回合记录。
/// </summary>
public sealed class TurnRecord
{
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;

    public string UserInput { get; init; } = string.Empty;

    public string AssistantOutput { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public IReadOnlyList<StepRecord> Steps { get; init; } = Array.Empty<StepRecord>();
}

/// <summary>
/// 单个步骤执行记录。
/// </summary>
public sealed class StepRecord
{
    public int Order { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string? Instruction { get; init; } = string.Empty;

    public string? ArgumentsJson { get; init; }

    public string Output { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}
