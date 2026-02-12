using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Execution
{
    /// <summary>
    /// 【Execution 层 - 计划执行器】
    /// 按顺序执行 AgentPlan 中的每个 PlanStep，是 Runtime 流程中的"执行引擎"。
    /// 
    /// 核心职责：
    /// 1. 为每个 PlanStep 创建独立的 StepContext（不污染 Root 上下文）。
    /// 2. 通过 RouterAgent 将 StepContext 路由到目标 Agent 执行。
    /// 3. 将每个 Step 的执行状态合并回会话级 ConversationState。
    /// 4. 处理失败终止和动态路由。
    /// 
    /// 在运行时流程中的位置：
    /// AgentRuntimeService.RunAsync → PlannerAgent.CreatPlanAsync → PlanExecutor.ExecuteAsync
    /// </summary>
    public sealed class PlanExecutor
    {
        /// <summary>路由 Agent，负责将 StepContext 分发到目标 Agent。</summary>
        public readonly RouterAgent _router;

        /// <summary>
        /// 初始化计划执行器。
        /// </summary>
        /// <param name="router">路由 Agent 实例。</param>
        public PlanExecutor(RouterAgent router)
        {
            _router = router;
        }

        /// <summary>
        /// 按步骤顺序执行整个计划。
        /// 
        /// 流程：
        /// 1. 将 Run 状态设置为 Executing。
        /// 2. 按 Order 排序遍历所有 PlanStep。
        /// 3. 为每个 Step 创建独立 StepContext 并通过 RouterAgent 执行。
        /// 4. 执行成功则合并 State；失败则立即终止整个 Run。
        /// 5. 所有步骤完成后拼接 FinalOutput 并同步状态回 Root。
        /// </summary>
        /// <param name="run">当前运行上下文，包含计划、状态等信息。</param>
        public async Task ExecuteAsync(AgentRunContext run)
        {
            // 1. 校验计划是否存在
            if (run.Plan == null)
                throw new InvalidOperationException("No plan available for execution.");

            // 2. 标记 Run 状态为"执行中"
            run.Status = AgentRunStatus.Executing;

            // 3. 按步骤顺序排列
            var steps = run.Plan.Steps.OrderBy(d => d.Order).ToList();

            // 4. 逐步执行
            foreach (var step in steps)
            {
                // 4.1 检查取消令牌
                run.Root.CancellationToken.ThrowIfCancellationRequested();

                // 4.2 创建步骤执行跟踪对象，写入 Run 的步骤列表
                var exec = new PlanStepExecution
                {
                    Step = step,
                    Status = StepExecutionStatus.Running
                };
                run.Steps.Add(exec);

                try
                {
                    // 4.3 创建独立的 StepContext（继承 ConversationState，但 Input 替换为 Step.Instruction）
                    var stepContext = CreateStepContext(run, step);

                    // 4.4 通过 RouterAgent 路由到目标 Agent 执行
                    var result = await _router.ExecuteAsync(stepContext);

                    // 4.5 记录执行结果和状态
                    exec.Output = result.Output;
                    exec.Status = result.IsSuccess ? StepExecutionStatus.Success : StepExecutionStatus.Failed;

                    // 4.6 合并 Step 的 State 回会话级 ConversationState（用于后续步骤和记忆）
                    MergeState(run.ConversationState, stepContext.State);

                    // 4.7 如果执行失败，立即终止整个 Run
                    if (!result.IsSuccess)
                    {
                        exec.Error = exec.Error ?? "AgentResult.IsSuccess=false";
                        run.Status = AgentRunStatus.Failed;
                        run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
                        run.SyncStateBackToRoot();
                        return;
                    }

                    // 4.8 支持动态路由：如果 Agent 返回了 NextAgent，覆盖后续步骤的目标
                    if (!string.IsNullOrWhiteSpace(result.NextAgent))
                    {
                        run.ConversationState["next_agent_override"] = result.NextAgent!;
                    }
                }
                catch (Exception ex)
                {
                    // 4.9 异常处理：记录错误并终止 Run
                    exec.Error = ex.Message;
                    exec.Status = StepExecutionStatus.Failed;
                    run.Status = AgentRunStatus.Failed;
                    run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
                    run.SyncStateBackToRoot();
                    return;
                }
            }

            // 5. 所有步骤执行完毕，拼接最终输出并标记完成
            run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
            run.Status = AgentRunStatus.Completed;
            run.SyncStateBackToRoot();
        }

        /// <summary>
        /// 为单个 PlanStep 创建独立的 StepContext。
        /// 
        /// 关键设计（SSOT 原则）：
        /// - Input 被替换为 PlanStep.Instruction，而非用户原始输入。
        /// - 从 ConversationState 复制共享数据（profile/recent_turns 等）。
        /// - 保留 user_input 和 conversation_id 供 Agent 需要时使用。
        /// - 支持 next_agent_override 动态路由覆盖。
        /// </summary>
        /// <param name="run">当前运行上下文。</param>
        /// <param name="step">当前要执行的计划步骤。</param>
        /// <returns>独立的步骤执行上下文。</returns>
        private static AgentContext CreateStepContext(AgentRunContext run, PlanStep step)
        {
            // 1. 确定目标 Agent（优先使用动态路由覆盖）
            var target = step.Agent;
            if (run.ConversationState.TryGetValue("next_agent_override", out var next) && next is string s && !string.IsNullOrWhiteSpace(s))
            {
                target = s;
                run.ConversationState.Remove("next_agent_override");
            }

            // 2. 创建独立的 AgentContext，共享 RequestId 和 CancellationToken
            var ctx = new AgentContext
            {
                RequestId = run.Root.RequestId,
                CancellationToken = run.Root.CancellationToken,
                Target = target,
                Input = step.Instruction,
                ExpectedOutput = step.ExpectedOutput
            };

            // 3. 注入会话级共享 State（Memory/Profile/Persona 等）
            foreach (var kv in run.ConversationState)
                ctx.State[kv.Key] = kv.Value;

            // 4. 保留原始用户输入和会话 ID（方便 Agent 需要时读取）
            ctx.State["user_input"] = run.UserInput;
            ctx.State["conversation_id"] = run.ConversationId;

            return ctx;
        }

        /// <summary>
        /// 将 Step 执行后的 State 合并回会话级 ConversationState。
        /// 使用简单覆盖策略：stepState 中的同名 key 覆盖会话级 State。
        /// </summary>
        /// <param name="conversationState">会话级共享状态。</param>
        /// <param name="stepState">步骤执行后的状态。</param>
        private static void MergeState(Dictionary<string, object> conversationState, Dictionary<string, object> stepState)
        {
            foreach (var kv in stepState)
                conversationState[kv.Key] = kv.Value;
        }

    }
}
