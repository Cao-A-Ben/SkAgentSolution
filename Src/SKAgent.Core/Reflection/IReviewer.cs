namespace SKAgent.Core.Reflection;

public interface IReviewer
{
    Task<RepairPlanDecision> ReviewFailureAsync(
        FailureReviewRequest request,
        CancellationToken ct);
}
