using SKAgent.Core.Planning;

namespace SKAgent.Core.Reflection;

public enum FailureSource
{
    Planner = 0,
    Executor = 1,
    Tool = 2,
    Memory = 3
}

public enum RepairStepStatus
{
    Planned = 0,
    Running = 1,
    Completed = 2,
    Skipped = 3,
    Failed = 4
}

public sealed record PlanDiff(
    int Added,
    int Removed,
    int Modified,
    string Summary
);

public sealed record RepairPlanStep(
    string Id,
    string Title,
    string Action,
    string? Target,
    RepairStepStatus Status,
    string? Notes);

public sealed record FailureReviewRequest(
    ReflectionContext Run,
    FailureSource FailureSource,
    string FailurePhase,
    ErrorInfo Error,
    int Attempt,
    int MaxRetries,
    PlanStep? FailedStep = null,
    int? FailedOrder = null);

public sealed record RepairPlanDecision(
    bool ShouldRepair,
    FailureSource FailureSource,
    string FailureCategory,
    string Reason,
    string FailurePhase,
    int? FailedOrder,
    IReadOnlyList<RepairPlanStep> RepairSteps,
    PlanDiff? Diff = null,
    IReadOnlyList<PlanStep>? RepairedSteps = null);
