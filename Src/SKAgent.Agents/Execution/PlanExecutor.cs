using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Observability;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Reflection;
using SKAgent.Agents.Runtime;
using SKAgent.Agents.Tools.Abstractions;
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

        /// <summary>工具调用器，负责执行 Kind=Tool 的步骤。</summary>
        private readonly IToolInvoker _toolInvoker;

        private readonly IReflectionAgent _reflectionAgent;

        /// <summary>
        /// 初始化计划执行器。
        /// </summary>
        /// <param name="router">路由 Agent 实例。</param>
        /// <param name="toolInvoker">工具调用器实例。</param>
        public PlanExecutor(RouterAgent router, IToolInvoker toolInvoker, IReflectionAgent reflectionAgent)
        {
            _router = router;
            _toolInvoker = toolInvoker;
            _reflectionAgent = reflectionAgent;
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
                await run.EmitAsync("step_started", new { order = step.Order, kind = step.Kind.ToString(), target = step.Target }, run.Root.CancellationToken);

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

                    #region tool
                    if (step.Kind == PlanStepKind.Tool)
                    {
                        // Tool Step 执行路径

                        var toolName = step.Target ?? throw new InvalidOperationException("Tool step missing ToolName.");
                        var argsjson = step.ArgumentsJson ?? "{}";

                        using var doc = JsonDocument.Parse(argsjson);
                        var args = doc.RootElement;

                        var invocation = new ToolInvocation(
                            RunId: run.Root.RequestId,
                            StepId: step.Order.ToString(),
                            ToolName: toolName,
                            Arguments: args
                            );

                        var result = await _toolInvoker.InvokeAsync(invocation, run.Root.CancellationToken);

                        // 写入Exec (SSOT)
                        exec.Output = result.Output.ToString();
                        exec.Status = result.Success ? StepExecutionStatus.Success : StepExecutionStatus.Failed;

                        if (!result.Success)
                        {
                            exec.Error = result.Error?.Message ?? "ToolResult.Sucess=false";
                        }

                        // 写入run.ToolCalls (SSOT)
                        run.ToolCalls.Add(new ToolCallRecord
                        (
                            StepOrder: step.Order,
                            ToolName: toolName,
                            ArgsPreview: PreviewJson(argsjson, 300),
                            Success: result.Success,
                            OutputPreview: exec.Output,
                            ErrorCode: result.Error?.Code,
                            ErrorMessage: result.Error?.Message,
                            LatencyMs: result.Metrics?.LatencyMs ?? 0
                            ));


                        // 失败： 先终止 再接反思重试
                        if (!result.Success)
                        {

                            var attempt = run.StepRetryCounts.ContainsKey(step.Order) ? run.StepRetryCounts[step.Order] : 0;

                            // 使用 ReflectionAgent 决定是否重试
                            var reflectionDecision = await _reflectionAgent.DecideAsync(run, step, "Tool step failed", run.Root.CancellationToken);

                            if (reflectionDecision.Action == ReflectionDecisionKind.RetrySameStep && attempt < 3)// 设置最大重试次数
                            {
                                run.StepRetryCounts[step.Order] = attempt + 1;

                                await run.EmitAsync("retry_scheduled", new
                                {
                                    order = step.Order,
                                    reason = reflectionDecision.Reason,
                                    attempt = attempt + 1,
                                    max = 3
                                }, run.Root.CancellationToken);
                                continue; // 跳过当前失败步骤，重试
                            }

                            //达到最大重试次数或不重试，终止Run
                            await run.EmitAsync("step_failed", new
                            {
                                order = step.Order,
                                success = false,
                                error = exec.Error
                            }, run.Root.CancellationToken);


                            run.Status = AgentRunStatus.Failed;
                            run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
                            run.SyncStateBackToRoot();
                            return;//终止执行
                        }
                        await run.EmitAsync("step_completed", new
                        {
                            order = step.Order,
                            success = true,
                            outputPreview = exec.Output
                        }, run.Root.CancellationToken);


                        continue;
                    }

                    #endregion


                    #region Agent


                    // 4.3 创建独立的 StepContext（继承 ConversationState，但 Input 替换为 Step.Instruction）
                    var stepContext = CreateStepContext(run, step);

                    // 4.4 通过 RouterAgent 路由到目标 Agent 执行
                    var resultAgent = await _router.ExecuteAsync(stepContext);

                    // 4.5 记录执行结果和状态
                    exec.Output = resultAgent.Output;
                    exec.Status = resultAgent.IsSuccess ? StepExecutionStatus.Success : StepExecutionStatus.Failed;

                    // 4.6 合并 Step 的 State 回会话级 ConversationState（用于后续步骤和记忆）
                    MergeState(run.ConversationState, stepContext.State);

                    // 4.7 如果执行失败，立即终止整个 Run
                    if (!resultAgent.IsSuccess)
                    {
                        //处理失败并反思重试
                        var attempt = run.StepRetryCounts.ContainsKey(step.Order) ? run.StepRetryCounts[step.Order] : 0;

                        // 使用 ReflectionAgent 决定是否重试
                        var reflectionDecision = await _reflectionAgent.DecideAsync(run, step, "Tool step failed", run.Root.CancellationToken);
                        if (reflectionDecision.Action == ReflectionDecisionKind.RetrySameStep && attempt < 3)// 设置最大重试次数
                        {
                            run.StepRetryCounts[step.Order] = attempt + 1;

                            await run.EmitAsync("retry_scheduled", new
                            {
                                order = step.Order,
                                reason = reflectionDecision.Reason,
                                attempt = attempt + 1,
                                max = 3
                            }, run.Root.CancellationToken);
                            continue; // 跳过当前失败步骤，重试
                        }


                        await run.EmitAsync("step_failed", new
                        {
                            order = step.Order,
                            success = false,
                            error = exec.Error
                        }, run.Root.CancellationToken);


                        exec.Error = exec.Error ?? "AgentResult.IsSuccess=false";
                        run.Status = AgentRunStatus.Failed;
                        run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
                        run.SyncStateBackToRoot();
                        return;
                    }

                    // 4.8 支持动态路由：如果 Agent 返回了 NextAgent，覆盖后续步骤的目标
                    if (!string.IsNullOrWhiteSpace(resultAgent.NextAgent))
                    {
                        run.ConversationState["next_agent_override"] = resultAgent.NextAgent!;
                    }

                    await run.EmitAsync("step_completed", new
                    {
                        order = step.Order,
                        success = true,
                        outputPreview = exec.Output
                    }, run.Root.CancellationToken);
                    #endregion

                }
                catch (Exception ex)
                {
                    await run.EmitAsync("step_failed", new
                    {
                        order = step.Order,
                        success = false,
                        error = ex.Message
                    }, run.Root.CancellationToken);
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
            var target = step.Target;
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
                Input = step.Instruction ?? "",
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

        /// <summary>
        /// 将 JsonElement 转为字符串并截断到指定长度，避免大 JSON 占用过多空间。
        /// </summary>
        private static string SafePreview(System.Text.Json.JsonElement output)
        {
            var s = output.ToString() ?? "";
            return PreviewJson(s, 500);
        }

        /// <summary>
        /// 将字符串截断到指定长度，超出部分以 "..." 结尾。
        /// </summary>
        private static string PreviewJson(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

    }
}
