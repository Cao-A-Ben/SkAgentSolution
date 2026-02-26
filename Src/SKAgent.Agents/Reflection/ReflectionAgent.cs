using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;

namespace SKAgent.Agents.Reflection
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
        public Task<ReflectionDecision> DecideAsync(AgentRunContext run, PlanStep step, string reason, CancellationToken ct)
        {

            //最小策略：线虫是同一步（由 RetryPolicy 限制次数）
            return Task.FromResult(new ReflectionDecision(ReflectionDecisionKind.RetrySameStep, reason));
        }
    }
}
