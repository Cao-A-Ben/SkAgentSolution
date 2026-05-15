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

            steps.AddRange(request.FailureSource switch
            {
                FailureSource.Tool => BuildToolRepairSteps(classification, request, failedTarget),
                FailureSource.Planner => BuildPlannerRepairSteps(classification, request),
                FailureSource.Memory => BuildMemoryRepairSteps(classification, request),
                _ => BuildExecutorRepairSteps(classification, request, failedTarget)
            });

            return steps;
        }

        private static IReadOnlyList<RepairPlanStep> BuildToolRepairSteps(
            ErrorClassification classification,
            FailureReviewRequest request,
            string? failedTarget)
        {
            if (classification.Category == "tool_not_found")
            {
                return
                [
                    new RepairPlanStep(
                        Id: "validate_tool_registry",
                        Title: "Validate tool registry and allowlist",
                        Action: "validate_tool_registry",
                        Target: failedTarget,
                        Status: RepairStepStatus.Planned,
                        Notes: $"Confirm whether tool '{failedTarget ?? "unknown"}' is registered, enabled, and allowed for this runtime."),
                    new RepairPlanStep(
                        Id: "replan_with_registered_tool",
                        Title: "Rebuild the step with a registered tool",
                        Action: "replan_with_registered_tool",
                        Target: failedTarget,
                        Status: RepairStepStatus.Planned,
                        Notes: "Do not retry blindly; replace the missing tool with a supported tool target before rerunning the plan.")
                ];
            }

            if (classification.Category == "bad_request")
            {
                return
                [
                    new RepairPlanStep(
                        Id: "validate_tool_arguments",
                        Title: "Validate tool arguments against schema",
                        Action: "validate_tool_arguments",
                        Target: failedTarget,
                        Status: RepairStepStatus.Planned,
                        Notes: $"Check required fields, argument types, and serialized JSON for tool '{failedTarget ?? "unknown"}'."),
                    new RepairPlanStep(
                        Id: "rebuild_step_with_schema_checked_args",
                        Title: "Rebuild the tool step with corrected arguments",
                        Action: "rebuild_step_with_schema_checked_args",
                        Target: failedTarget,
                        Status: RepairStepStatus.Planned,
                        Notes: "After validating the schema, regenerate only the failed tool step instead of replaying the whole run.")
                ];
            }

            if (classification.Category is "timeout" or "rate_limited" or "transient_network")
            {
                return
                [
                    new RepairPlanStep(
                        Id: "stabilize_tool_runtime",
                        Title: "Inspect tool runtime health and timeout budget",
                        Action: "inspect_tool_runtime_health",
                        Target: failedTarget,
                        Status: RepairStepStatus.Planned,
                        Notes: $"Check provider health, timeout budget, and network path for tool '{failedTarget ?? "unknown"}' before any retry."),
                    new RepairPlanStep(
                        Id: "retry_same_tool_step_with_backoff",
                        Title: "Prepare a controlled retry with backoff",
                        Action: "retry_same_tool_step_with_backoff",
                        Target: failedTarget,
                        Status: RepairStepStatus.Planned,
                        Notes: "Week11 keeps this as a recorded recommendation only; automatic repair execution remains disabled.")
                ];
            }

            return
            [
                new RepairPlanStep(
                    Id: "inspect_tool_contract",
                    Title: "Inspect tool contract and output expectations",
                    Action: "inspect_tool_contract",
                    Target: failedTarget,
                    Status: RepairStepStatus.Planned,
                    Notes: $"Review the tool contract for '{failedTarget ?? "unknown"}' and confirm the current plan is using the right tool for the task."),
                new RepairPlanStep(
                    Id: "revise_tool_selection_or_args",
                    Title: "Revise tool selection or arguments",
                    Action: "revise_tool_selection_or_args",
                    Target: failedTarget,
                    Status: RepairStepStatus.Planned,
                    Notes: "Treat this as a non-transient tool failure. Prefer replanning the failed step over brute-force retries.")
            ];
        }

        private static IReadOnlyList<RepairPlanStep> BuildPlannerRepairSteps(
            ErrorClassification classification,
            FailureReviewRequest request)
        {
            var failedTarget = request.FailedStep?.Target;

            return
            [
                new RepairPlanStep(
                    Id: "validate_planner_prompt_contract",
                    Title: "Validate planner prompt and JSON contract",
                    Action: "validate_planner_prompt_contract",
                    Target: failedTarget,
                    Status: RepairStepStatus.Planned,
                    Notes: $"Review planner prompt structure, output schema, and model response shape. Failure category={classification.Category}."),
                new RepairPlanStep(
                    Id: "rebuild_plan_from_simplified_input",
                    Title: "Rebuild the plan from simplified planner input",
                    Action: "rebuild_plan_from_simplified_input",
                    Target: failedTarget,
                    Status: RepairStepStatus.Planned,
                    Notes: "Trim noisy context, keep the required task and constraints, then regenerate the failed plan. Automatic plan repair stays disabled in Week11.")
            ];
        }

        private static IReadOnlyList<RepairPlanStep> BuildMemoryRepairSteps(
            ErrorClassification classification,
            FailureReviewRequest request)
        {
            return
            [
                new RepairPlanStep(
                    Id: "inspect_retrieval_plan_inputs",
                    Title: "Inspect retrieval plan and memory inputs",
                    Action: "inspect_retrieval_plan_inputs",
                    Target: "memory_bundle",
                    Status: RepairStepStatus.Planned,
                    Notes: $"Validate persona, retrieval routes, profile snapshot, and recent turns before rebuilding memory. Failure category={classification.Category}."),
                new RepairPlanStep(
                    Id: "fallback_to_recent_history_only",
                    Title: "Prepare a fallback memory bundle from recent history",
                    Action: "fallback_to_recent_history_only",
                    Target: "memory_bundle",
                    Status: RepairStepStatus.Planned,
                    Notes: $"If vector/facts/profile enrichment remains unstable, rebuild the run with a reduced memory scope for '{request.Run.UserInput}'.")
            ];
        }

        private static IReadOnlyList<RepairPlanStep> BuildExecutorRepairSteps(
            ErrorClassification classification,
            FailureReviewRequest request,
            string? failedTarget)
        {
            return
            [
                new RepairPlanStep(
                    Id: "inspect_failed_agent_step",
                    Title: "Inspect the failed agent step output and state",
                    Action: "inspect_failed_agent_step",
                    Target: failedTarget,
                    Status: RepairStepStatus.Planned,
                    Notes: $"Check the failed agent step, working memory, and merged conversation state before deciding whether to retry. Failure category={classification.Category}."),
                new RepairPlanStep(
                    Id: "freeze_remaining_steps_and_replan",
                    Title: "Freeze the remaining steps and replan from the failed point",
                    Action: "freeze_remaining_steps_and_replan",
                    Target: failedTarget,
                    Status: RepairStepStatus.Planned,
                    Notes: "Do not continue the remaining steps as-is. Rebuild the downstream plan after the failed executor step is understood.")
            ];
        }
    }
}
