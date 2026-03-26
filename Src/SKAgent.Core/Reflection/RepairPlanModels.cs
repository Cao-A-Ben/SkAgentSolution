using SKAgent.Core.Planning;

namespace SKAgent.Core.Reflection;

public sealed record PlanDiff(
    int Added,
    int Removed,
    int Modified,
    string Summary
);

public sealed record RepairPlanDecision(
    bool ShouldRepair,
    string Reason,
    PlanDiff? Diff,
    IReadOnlyList<PlanStep>? RepairedSteps = null
);
