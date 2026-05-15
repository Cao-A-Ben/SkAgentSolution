using SKAgent.Core.Planning;
using SKAgent.Core.Reflection;
using SKAgent.Core.Runtime;

namespace SKAgent.Runtime.Utilities;

internal static class RepairEventEmitter
{
    public static async Task EmitRepairPlanAsync(
        IRunContext run,
        RepairPlanDecision decision,
        PlanStep? failedStep,
        CancellationToken ct)
    {
        await run.EmitAsync("repair_plan_created", new
        {
            failureSource = ToFailureSourceKey(decision.FailureSource),
            failureCategory = decision.FailureCategory,
            reason = decision.Reason,
            failedPhase = decision.FailurePhase,
            failedOrder = decision.FailedOrder,
            failedKind = failedStep?.Kind.ToString(),
            failedTarget = failedStep?.Target,
            repairStepCount = decision.RepairSteps.Count,
            repairSteps = decision.RepairSteps.Select(step => new
            {
                id = step.Id,
                title = step.Title,
                action = step.Action,
                target = step.Target,
                status = ToRepairStatusKey(step.Status),
                notes = step.Notes
            })
        }, ct);

        await EmitRepairWorkflowStepAsync(
            run,
            repairStepId: "collect_failure_context",
            title: "Collect failure context",
            action: "collect_failure_context",
            target: failedStep?.Target ?? ToFailureSourceKey(decision.FailureSource),
            notes: "Capture failure metadata and replay evidence before proposing a repair plan.",
            ct);

        await EmitRepairWorkflowStepAsync(
            run,
            repairStepId: "publish_repair_plan",
            title: "Publish repair plan",
            action: "publish_repair_plan",
            target: failedStep?.Target ?? ToFailureSourceKey(decision.FailureSource),
            notes: "Record the recommended repair steps without automatically applying them in Week11 phase 1.",
            ct);
    }

    private static async Task EmitRepairWorkflowStepAsync(
        IRunContext run,
        string repairStepId,
        string title,
        string action,
        string? target,
        string notes,
        CancellationToken ct)
    {
        await run.EmitAsync("repair_step_started", new
        {
            repairStepId,
            title,
            action,
            target,
            status = ToRepairStatusKey(RepairStepStatus.Running),
            notes
        }, ct);

        await run.EmitAsync("repair_step_completed", new
        {
            repairStepId,
            title,
            action,
            target,
            status = ToRepairStatusKey(RepairStepStatus.Completed),
            notes
        }, ct);
    }

    internal static string ToFailureSourceKey(FailureSource source)
        => source.ToString().ToLowerInvariant();

    internal static string ToRepairStatusKey(RepairStepStatus status)
        => status.ToString().ToLowerInvariant();
}
