using System.Text.Json;
using SKAgent.Application.Runtime;
using SKAgent.Core.Agent;
using SKAgent.Core.Execution;
using SKAgent.Core.Planning;
using SKAgent.Core.Reflection;
using SKAgent.Core.Routing;
using SKAgent.Core.Runtime;
using SKAgent.Core.Tools.Abstractions;
using SKAgent.Runtime;
using SKAgent.Runtime.Utilities;

namespace SkAgent.Runtime.Execution
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
        #region Fields

        #region PlanExecutor 不应该在执行阶段做 persona/memory 的选择与事件，它只消费 run.ConversationState

        /// <summary>Week6 人格选择管理器：决定本次运行使用的人格。</summary>
        //private readonly PersonaManager _personaManager;

        /// <summary>Week6 记忆编排器：在执行前构建三层记忆包。</summary>
        //private readonly MemoryOrchestrator _memoryOrchestrator; 
        #endregion

        /// <summary>路由 Agent，负责将 StepContext 分发到目标 Agent。</summary>
        public readonly IStepRouter _router;

        /// <summary>工具调用器，负责执行 Kind=Tool 的步骤。</summary>
        private readonly IToolInvoker _toolInvoker;

        /// <summary>反思 Agent，用于处理失败后的重试决策。</summary>
        private readonly IReflectionAgent _reflectionAgent;

        /// <summary>单步最大重试次数。</summary>
        const int MaxRetriesPerStep = 3;
        #endregion

        #region Ctor

        /// <summary>
        /// 初始化计划执行器。
        /// </summary>
        /// <param name="router">路由 Agent 实例。</param>
        /// <param name="toolInvoker">工具调用器实例。</param>
        /// <param name="reflectionAgent">反思 Agent 实例。</param>
        /// <param name="personaManager">人格管理器实例。</param>
        public PlanExecutor(IStepRouter router, IToolInvoker toolInvoker,
            IReflectionAgent reflectionAgent)
        {
            _router = router;
            _toolInvoker = toolInvoker;
            _reflectionAgent = reflectionAgent;
            //_personaManager = personaManager;
            //_memoryOrchestrator = memoryOrchestrator;
        }
        #endregion

        #region Public API
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


            // ===== Week6：人格选择开始 =====
            //var runId = run.RunId;
            //var conversationId = run.ConversationId;
            //string? requestedPersonaName = null;

            //var sel = _personaManager.GetOrSelect(runId, conversationId, requestedPersonaName);

            //// 写回状态：后续任何 Agent/Composer 都能读到
            //run.Root.State["personaName"] = sel.Persona.Name;
            //run.Root.State["persona"] = sel.Persona; // 可选：不想在 state 放对象就删掉这行

            //await run.EmitAsync("persona_selected", new
            //{
            //    conversationId,
            //    personaName = sel.Persona.Name,
            //    source = sel.Source,
            //    reason = sel.Reason
            //}, run.Root.CancellationToken);

            if (!run.ConversationState.ContainsKey("persona"))
                throw new InvalidOperationException("Missing persona. AgentRuntimeService must call IRunPreparationService.PrepareAsync before ExecuteAsync.");

         
            // ===== Persona 选择结束 =====

            // ===== Week6：记忆构建开始 =====
            //var persona = (PersonaOptions)run.Root.State["persona"]; // 或根据存储方式拿到 persona
            //var input = run.Root.Input ?? run.Plan.Goal; // 按真实字段取
            //var bundle = await _memoryOrchestrator.BuildAsync(run, persona, input, run.Root.CancellationToken);

            // W6-3 会用到：先放进 state
            //run.Root.State["memoryBundle"] = bundle;
           // run.ConversationState["memoryBundle"] = bundle;
            if (!run.ConversationState.ContainsKey("memoryBundle"))
                throw new InvalidOperationException("Missing memoryBundle. AgentRuntimeService must call IRunPreparationService.PrepareAsync before ExecuteAsync.");

            //===== Week6：记忆构建结束 =====

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
                // 统一：每个 step 都走“尝试执行 + 失败反思 + 重试/终止”的 runner
                var ok = await ExecuteStepWithRetryAsync(run, step, exec);
                if (!ok) return; // runner 内部已 run_failed + SyncStateBackToRoot
            }

            // 5. 所有步骤执行完毕，拼接最终输出并标记完成
            run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
            run.Status = AgentRunStatus.Completed;
            run.SyncStateBackToRoot();

            await run.EmitAsync("run_completed", new
            {
                finalOutput = run.FinalOutput ?? "",
                stepCount = run.Steps.Count,
                retries = run.StepRetryCounts,
                toolCallCount = run.ToolCalls.Count
            }, run.Root.CancellationToken);
        }
        #endregion




        #region Core Runner (Retry + Reflection)

        private async Task<bool> ExecuteStepWithRetryAsync(AgentRunContext run, PlanStep step, PlanStepExecution exec)
        {
            while (true)
            {
                try
                {
                    if (step.Kind == PlanStepKind.Tool)
                    {
                        var toolAttempt = await TryExecuteToolStepAsync(run, step, exec);
                        if (toolAttempt.Success)
                        {
                            await run.EmitAsync("step_completed",
                                new { order = step.Order, success = true, outputPreview = TextPreview.Preview(exec.Output, 600) },
                                run.Root.CancellationToken);
                            return true;
                        }

                        var attempt = GetAttempt(run, step.Order);
                        var shouldContinue = await HandleFailureAndMaybeRetryAsync(
                            run: run,
                            step: step,
                            exec: exec,
                            failurePhase: "tool",
                            error: toolAttempt.Error!,
                            attempt: attempt);

                        if (shouldContinue) continue;
                        return false;
                    }
                    else
                    {
                        var agentAttempt = await TryExecuteAgentStepAsync(run, step, exec);
                        if (agentAttempt.Success)
                        {
                            await run.EmitAsync("step_completed",
                                new { order = step.Order, success = true, outputPreview = TextPreview.Preview(exec.Output, 600) },
                                run.Root.CancellationToken);
                            return true;
                        }

                        var attempt = GetAttempt(run, step.Order);
                        var shouldContinue = await HandleFailureAndMaybeRetryAsync(
                            run: run,
                            step: step,
                            exec: exec,
                            failurePhase: "agent",
                            error: agentAttempt.Error!,
                            attempt: attempt);

                        if (shouldContinue) continue;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    exec.Error = ex.Message;
                    exec.Status = StepExecutionStatus.Failed;

                    UpdateWorkingMemoryForStep(run, step, exec);

                    await run.EmitAsync("step_failed", new
                    {
                        order = step.Order,
                        success = false,
                        error = ex.Message
                    }, run.Root.CancellationToken);

                    await FailRunAsync(run, step.Order, ex.Message);
                    return false;
                }
            }
        }

        private async Task<bool> HandleFailureAndMaybeRetryAsync(
            AgentRunContext run,
            PlanStep step,
            PlanStepExecution exec,
            string failurePhase, // "tool" | "agent"
            ErrorInfo error,
            int attempt)
        {
            // 统一 step_failed（你之前两条路径都有）
            await run.EmitAsync("step_failed", new
            {
                order = step.Order,
                success = false,
                error = exec.Error
            }, run.Root.CancellationToken);

            // reflection_triggered（保持字段一致）
            await EmitReflectionAsync(
                run, step,
                phase: failurePhase,
                attempt: attempt,
                max: MaxRetriesPerStep,
                errorCode: error.Code,
                httpStatus: error.HttpStatus,
                errorMessage: exec.Error ?? error.Message);

            // 反思决策：用 ReflectionContext（避免 Core 反依赖 Application）
            var rctx = ReflectionContextBuilder.Build(run);

            var decision = await _reflectionAgent.DecideAsync(
                rctx,
                step,
                failurePhase: failurePhase,
                error: error,
                attempt: attempt,
                maxRetries: MaxRetriesPerStep,
                ct: run.Root.CancellationToken);

            if (decision.Action == ReflectionDecisionKind.RetrySameStep && attempt < MaxRetriesPerStep)
            {
                SetAttempt(run, step.Order, attempt + 1);

                await run.EmitAsync("retry_scheduled", new
                {
                    order = step.Order,
                    reason = decision.Reason,
                    attempt = attempt + 1,
                    max = MaxRetriesPerStep
                }, run.Root.CancellationToken);

                return true; // continue while => retry
            }

            await run.EmitAsync("retry_skipped", new
            {
                order = step.Order,
                category = decision.Category,
                reason = decision.Reason,
                attempt,
                max = MaxRetriesPerStep
            }, run.Root.CancellationToken);

            await FailRunAsync(run, step.Order, exec.Error ?? error.Message);
            return false;
        }

        #endregion

        #region Step Attempts (Tool / Agent)

        private async Task<(bool Success, ErrorInfo? Error)> TryExecuteToolStepAsync(
            AgentRunContext run,
            PlanStep step,
            PlanStepExecution exec)
        {
            var toolName = step.Target ?? throw new InvalidOperationException("Tool step missing ToolName.");
            var argsJson = step.ArgumentsJson ?? "{}";

            using var doc = JsonDocument.Parse(argsJson);
            var args = doc.RootElement;

            var invocation = new ToolInvocation(
                RunId: run.Root.RequestId,
                StepId: step.Order.ToString(),
                ToolName: toolName,
                Arguments: args);

            await run.EmitAsync("tool_invoked", new
            {
                order = step.Order,
                toolName,
                argsPreview = TextPreview.Preview(argsJson, 300)
            }, run.Root.CancellationToken);

            var result = await _toolInvoker.InvokeAsync(invocation, run.Root.CancellationToken);

            await run.EmitAsync("tool_completed", new
            {
                order = step.Order,
                toolName,
                success = result.Success,
                latencyMs = result.Metrics?.LatencyMs ?? 0,
                outputPreview = TextPreview.Preview(result.Output.ToString(), 600)
            }, run.Root.CancellationToken);

            exec.Output = result.Output.ToString();
            exec.Status = result.Success ? StepExecutionStatus.Success : StepExecutionStatus.Failed;
            exec.Error = result.Success ? null : (result.Error?.Message ?? "Tool failed");

            // ToolCallRecord（SSOT）
            run.ToolCalls.Add(new ToolCallRecord(
                StepOrder: step.Order,
                ToolName: toolName,
                ArgsPreview: PreviewJson(argsJson, 300),
                Success: result.Success,
                OutputPreview: TextPreview.Preview(exec.Output, 600),
                ErrorCode: result.Error?.Code,
                ErrorMessage: result.Error?.Message,
                LatencyMs: result.Metrics?.LatencyMs ?? 0));

            // Working memory（先 step 再 tool）
            UpdateWorkingMemoryForStep(run, step, exec);
            UpdateWorkingMemoryForTool(run, step.Order, toolName, argsJson, result);

            if (result.Success) return (true, null);

            var err = new ErrorInfo(
                Code: result.Error?.Code,
                Message: exec.Error ?? result.Error?.Message ?? "Tool failed",
                HttpStatus: null);

            return (false, err);
        }

        private async Task<(bool Success, ErrorInfo? Error)> TryExecuteAgentStepAsync(
            AgentRunContext run,
            PlanStep step,
            PlanStepExecution exec)
        {

            var stepContext = CreateStepContext(run, step);

            stepContext.State["run"] = run;
            stepContext.State["conversation_id"] = run.ConversationId;
            var resultAgent = await _router.RouteAsync(stepContext, run.Root.CancellationToken);

            exec.Output = resultAgent.Output;
            exec.Status = resultAgent.IsSuccess ? StepExecutionStatus.Success : StepExecutionStatus.Failed;
            exec.Error = resultAgent.IsSuccess ? null : (exec.Error ?? "AgentResult.IsSuccess=false");

            // 合并 step state
            MergeState(run.ConversationState, stepContext.State);

            // Working memory（error 已填好）
            UpdateWorkingMemoryForStep(run, step, exec);

            if (resultAgent.IsSuccess)
            {
                if (!string.IsNullOrWhiteSpace(resultAgent.NextAgent))
                    run.ConversationState["next_agent_override"] = resultAgent.NextAgent!;

                return (true, null);
            }

            var err = new ErrorInfo(
                Code: "agent_error",
                Message: exec.Error ?? "Agent failed",
                HttpStatus: HttpStatusParser.TryParseHttpStatus(exec.Error));

            return (false, err);
        }

        #endregion

        #region Context + State
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

            ctx.State["run"] = run;
            ctx.State["persona"] = run.ConversationState["persona"];        // 或 persona 对象
            ctx.State["memoryBundle"] = run.ConversationState["memoryBundle"]; // W6-2 BuildAsync 后放进去的
            ctx.State["working_memory"] = run.ConversationState["working_memory"]; // 你原来用到的


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
        #endregion

        #region Working Memory
        /// <summary>
        /// 将步骤执行结果写入运行期工作记忆。
        /// </summary>
        private static void UpdateWorkingMemoryForStep(AgentRunContext run, PlanStep step, PlanStepExecution exec)
        {
            var wm = WorkingMemoryAccessor.GetOrCreate(run);

            var snap = new StepSnapshot(
                Order: step.Order,
                Kind: step.Kind.ToString(),
                Target: step.Target,
                OutputPreview: TextPreview.Preview(exec.Output, 600),
                Success: exec.Status == StepExecutionStatus.Success,
                Error: exec.Error
            );

            wm.LastStep = snap;
            wm.Steps[step.Order] = snap;
        }
        /// <summary>
        /// 将工具调用结果写入运行期工作记忆。
        /// </summary>
        private static void UpdateWorkingMemoryForTool(
            AgentRunContext run,
            int order,
            string toolName,
            string argsJson,
            ToolResult result)
        {
            var wm = WorkingMemoryAccessor.GetOrCreate(run);

            wm.LastTool = new ToolSnapshot(
                Order: order,
                ToolName: toolName,
                ArgsPreview: TextPreview.Preview(argsJson, 300),
                OutputPreview: TextPreview.Preview(result.Output.ToString(), 600),
                Success: result.Success,
                ErrorCode: result.Error?.Code,
                ErrorMessage: result.Error?.Message,
                LatencyMs: result.Metrics?.LatencyMs ?? 0
            );
        }

        #endregion

        #region Retry Counters

        /// <summary>
        /// 获取指定步骤的已重试次数。
        /// </summary>
        private static int GetAttempt(AgentRunContext run, int stepOrder)
            => run.StepRetryCounts.TryGetValue(stepOrder, out var v) ? v : 0;
        /// <summary>
        /// 记录指定步骤的重试次数。
        /// </summary>
        private static void SetAttempt(AgentRunContext run, int stepOrder, int attempt)
            => run.StepRetryCounts[stepOrder] = attempt;

        #endregion

        #region Emit + Fail
        /// <summary>
        /// 标记运行失败并发出 run_failed 事件。
        /// </summary>
        private static async Task FailRunAsync(AgentRunContext run, int failedOrder, string error)
        {
            run.Status = AgentRunStatus.Failed;
            run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
            run.SyncStateBackToRoot();

            //await run.EmitAsync("run_failed", new { failedOrder, error }, run.Root.CancellationToken);
            await run.EmitAsync("run_failed", new
            {
                finalOutput = run.FinalOutput ?? "",
                failedOrder,
                error,
                stepCount = run.Steps.Count,
                retries = run.StepRetryCounts
            }, run.Root.CancellationToken);
        }

        private static async Task EmitReflectionAsync(
            AgentRunContext run,
            PlanStep step,
            string phase, // "tool" | "agent"
            int attempt,
            int max,
            string? errorCode,
            int? httpStatus,
            string errorMessage)
        {
            await run.EmitAsync("reflection_triggered", new
            {
                order = step.Order,
                kind = step.Kind.ToString(),
                target = step.Target,
                phase,
                attempt,
                max,
                errorCode,
                httpStatus,
                error = errorMessage
            }, run.Root.CancellationToken);
        }

        #endregion

        #region Small Utils

        private static string PreviewJson(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        #endregion
    }
}
