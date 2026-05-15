using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Application.Runtime;
using SKAgent.Core.Planning;
using SKAgent.Core.Reflection;

namespace SKAgent.Application.Reflection
{

    /// <summary>
    /// 反思 Agent 的默认实现，负责根据失败原因给出重试/修复决策。
    /// </summary>
    public class ReflectionAgent : IReflectionAgent, IReviewer
    {
        /// <summary>
        /// 根据当前运行上下文与步骤状态生成反思决策。
        /// </summary>
        /// <param name="run">当前运行上下文。</param>
        /// <param name="step">当前步骤。</param>
        /// <param name="reason">触发反思的原因。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>反思决策。</returns>
        public Task<ReflectionDecision> DecideAsync(
            ReflectionContext run,
            PlanStep step,
            string failurePhases,
            ErrorInfo error,
            int attempt,
            int maxRetries,
            //string reason,
            CancellationToken ct)
        {

            var cls = ErrorClassifier.Classify(error);
            string stepHint = run.LastStep is null
      ? ""
      : $" last_step(order={run.LastStep.Order}, kind={run.LastStep.Kind}, target={run.LastStep.Target}, success={run.LastStep.Success}).";

            //不可重试
            if (cls.Retryability == Retryability.NonRetryable)
            {
                return Task.FromResult(new ReflectionDecision(ReflectionDecisionKind.None, cls.Reason+ stepHint, cls.Category));
            }

            //可重试
            if (cls.Retryability == Retryability.TransientRetryable && attempt < maxRetries)
            {
                return Task.FromResult(new ReflectionDecision(ReflectionDecisionKind.RetrySameStep,
                     $"{cls.Reason}{stepHint} Retry attempt {attempt + 1}/{maxRetries}.",
                    cls.Category));
            }


            // Unknown 保守不重试


            return Task.FromResult(new ReflectionDecision(
          ReflectionDecisionKind.None,
          cls.Retryability == Retryability.Unknown
              ? "Unknown error; choose not to retry by default."+ stepHint
              : "Retry limit reached.",
          cls.Category
      ));
            //最小策略：线虫是同一步（由 RetryPolicy 限制次数）
            //return Task.FromResult(new ReflectionDecision(ReflectionDecisionKind.RetrySameStep, reason));
        }

        public Task<RepairPlanDecision> ReviewFailureAsync(
            FailureReviewRequest request,
            CancellationToken ct)
        {
            var classification = ErrorClassifier.Classify(request.Error);
            var failedTarget = request.FailedStep?.Target;
            var steps = BuildRepairSteps(request, classification, failedTarget);

            return Task.FromResult(new RepairPlanDecision(
                ShouldRepair: true,
                FailureSource: request.FailureSource,
                FailureCategory: classification.Category,
                Reason: BuildRepairReason(request, classification),
                FailurePhase: request.FailurePhase,
                FailedOrder: request.FailedOrder,
                RepairSteps: steps));
        }

        private static string BuildRepairReason(FailureReviewRequest request, ErrorClassification classification)
        {
            var source = request.FailureSource.ToString().ToLowerInvariant();
            var stepTarget = string.IsNullOrWhiteSpace(request.FailedStep?.Target)
                ? string.Empty
                : $" Target={request.FailedStep!.Target}.";

            return $"{source} failure classified as {classification.Category}. {classification.Reason}{stepTarget}";
        }

        private static IReadOnlyList<RepairPlanStep> BuildRepairSteps(
            FailureReviewRequest request,
            ErrorClassification classification,
            string? failedTarget)
        {
            var steps = new List<RepairPlanStep>
            {
                new(
                    Id: "inspect_failure_context",
                    Title: "Inspect failure context",
                    Action: "inspect_failure_context",
                    Target: failedTarget,
                    Status: RepairStepStatus.Planned,
                    Notes: $"Capture {request.FailureSource.ToString().ToLowerInvariant()} failure details, attempt {request.Attempt}/{request.MaxRetries}, and replay evidence before changing behavior.")
            };

            steps.Add(request.FailureSource switch
            {
                FailureSource.Tool => BuildToolRepairStep(classification, request, failedTarget),
                FailureSource.Planner => BuildPlannerRepairStep(classification, request),
                FailureSource.Memory => BuildMemoryRepairStep(classification),
                _ => BuildExecutorRepairStep(classification, request, failedTarget)
            });

            return steps;
        }

        private static RepairPlanStep BuildToolRepairStep(
            ErrorClassification classification,
            FailureReviewRequest request,
            string? failedTarget)
        {
            var action = classification.Retryability == Retryability.TransientRetryable
                ? "retry_same_tool_step"
                : "revise_tool_selection_or_args";
            var title = classification.Retryability == Retryability.TransientRetryable
                ? "Retry the failed tool step"
                : "Revise tool selection or arguments";
            var notes = classification.Retryability == Retryability.TransientRetryable
                ? $"Prepare a controlled retry for tool '{failedTarget ?? "unknown"}' after inspecting timeout/network conditions. Automatic repair remains disabled in Week11."
                : $"Do not blindly retry tool '{failedTarget ?? "unknown"}'. Rebuild the plan or arguments because the failure looks non-transient.";

            return new RepairPlanStep(
                Id: "tool_repair_recommendation",
                Title: title,
                Action: action,
                Target: failedTarget,
                Status: RepairStepStatus.Planned,
                Notes: notes);
        }

        private static RepairPlanStep BuildPlannerRepairStep(
            ErrorClassification classification,
            FailureReviewRequest request)
        {
            var failedTarget = request.FailedStep?.Target;

            return new RepairPlanStep(
                Id: "planner_repair_recommendation",
                Title: "Rebuild the planner output",
                Action: "rebuild_plan",
                Target: failedTarget,
                Status: RepairStepStatus.Planned,
                Notes: $"Review the planner prompt, model output, and JSON contract. Failure category={classification.Category}; regenerate the plan only after validating the planner input.");
        }

        private static RepairPlanStep BuildMemoryRepairStep(ErrorClassification classification)
        {
            return new RepairPlanStep(
                Id: "memory_repair_recommendation",
                Title: "Re-run memory preparation with fallback",
                Action: "rebuild_memory_context",
                Target: "memory_bundle",
                Status: RepairStepStatus.Planned,
                Notes: $"Inspect retrieval routes, recall inputs, and fallback to recent history if needed. Failure category={classification.Category}.");
        }

        private static RepairPlanStep BuildExecutorRepairStep(
            ErrorClassification classification,
            FailureReviewRequest request,
            string? failedTarget)
        {
            return new RepairPlanStep(
                Id: "executor_repair_recommendation",
                Title: "Repair the failed executor step",
                Action: "rerun_or_replace_step",
                Target: failedTarget,
                Status: RepairStepStatus.Planned,
                Notes: $"Inspect the failed agent step output/state first, then decide whether to rerun or replace the remaining step. Failure category={classification.Category}.");
        }
    }
}
