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
    public class ReflectionAgent : IReflectionAgent
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
    }
}
