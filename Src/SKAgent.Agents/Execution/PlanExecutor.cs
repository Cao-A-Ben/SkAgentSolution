using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Execution
{
    public sealed class PlanExecutor
    {
        public readonly RouterAgent _router;
        private readonly IShortTermMemory _stm;

        public PlanExecutor(RouterAgent router, IShortTermMemory stm)
        {
            _router = router;
            _stm = stm;
        }


        public async Task ExecuteAsync(AgentRunContext run)
        {
            if (run.Plan == null)
                throw new InvalidOperationException("No plan available for execution.");

            run.Status = AgentRunStatus.Executing;
            //按步骤顺序执行
            var steps = run.Plan.Steps.OrderBy(d => d.Order).ToList();

            foreach (var step in steps)
            {
                run.Root.CancellationToken.ThrowIfCancellationRequested();
                //创建Step执行对象 写入上下文
                var exec = new PlanStepExecution
                {
                    Step = step,
                    Status = StepExecutionStatus.Running
                };
                run.Steps.Add(exec);

                try
                {

                    // StepContext 独立输入  不污染 Root上下文  也可选的共享部分Root上下文
                    var stepContext = CreateStepContext(run, step);

                    var result = await _router.ExecuteAsync(stepContext);

                    exec.Output = result.Output;
                    exec.Status = result.IsSuccess ? StepExecutionStatus.Success : StepExecutionStatus.Failed;


                    // 合并 Step State 回会话级 State (用于记忆/后续step)
                    MergeState(run.ConversationState, stepContext.State);

                    if (!result.IsSuccess)
                    {
                        exec.Error = exec.Error ?? "AgentResult.IsSuccess=false";
                        run.Status = AgentRunStatus.Failed;
                        run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
                        run.SyncStateBackToRoot();//可选：失败时也同步一次状态回Root，方便外部查看
                        return;//失败立即终止
                    }

                    // 可选: 支持动态路由
                    if (!string.IsNullOrWhiteSpace(result.NextAgent))
                    {
                        run.ConversationState["next_agent_override"] = result.NextAgent!;
                    }
                }
                catch (Exception ex)
                {
                    exec.Error = ex.Message;
                    exec.Status = StepExecutionStatus.Failed;
                    run.Status = AgentRunStatus.Failed;
                    run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
                    run.SyncStateBackToRoot();
                    return;
                }
            }
            run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
            run.Status = AgentRunStatus.Completed;
            run.SyncStateBackToRoot();
        }


        private static AgentContext CreateStepContext(AgentRunContext run, PlanStep step)
        {

            var target = step.Agent;
            if (run.ConversationState.TryGetValue("next_agent_override", out var next) && next is string s && !string.IsNullOrWhiteSpace(s))
            {
                target = s;
                run.ConversationState.Remove("next_agent_override");
            }

            var ctx = new AgentContext
            {
                RequestId = run.Root.RequestId, // 可选：同一 Run 共享 request id
                CancellationToken = run.Root.CancellationToken,
                Target = target,
                Input = step.Instruction,
                ExpectedOutput = step.ExpectedOutput
            };

            // ✅ 注入会话级 state（Memory/Profile/Tool 共享信息）
            foreach (var kv in run.ConversationState)
                ctx.State[kv.Key] = kv.Value;

            // ✅ 保留原始用户输入（方便某些 Agent 需要）
            ctx.State["user_input"] = run.UserInput;
            ctx.State["conversation_id"] = run.ConversationId;

            return ctx;
        }

        private static void MergeState(Dictionary<string, object> conversationState, Dictionary<string, object> stepState)
        {
            // 简单覆盖策略：stepState 覆盖会话级
            foreach (var kv in stepState)
                conversationState[kv.Key] = kv.Value;
        }

    }
}
