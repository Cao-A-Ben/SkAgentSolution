using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Planning;
using SKAgent.Agents.Runtime;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Execution
{
    public sealed class PlanExecutor
    {
        public readonly RouterAgent _router;

        public PlanExecutor(RouterAgent router)
        {
            _router = router;
        }


        public async Task ExecuteAsync(AgentRunContext run, CancellationToken ct = default)
        {
            if (run.Plan == null)
                throw new InvalidOperationException("No plan available for execution.");

            run.Status = AgentRunStatus.Executing;
            //按步骤顺序执行
            var steps = run.Plan.Steps.OrderBy(d => d.Order).ToList();

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                //创建Step执行对象 写入上下文
                var exec = new PlanStepExecution
                {
                    Step = step,
                    Status = StepExecutionStatus.Running
                };
                run.Steps.Add(exec);

                try
                {
                    // 标准协议写入
                    run.RootContext.Target = step.Agent;
                    run.RootContext.Input = step.Instruction;
                    run.RootContext.ExpectedOutput = step.ExpectedOutput;

                    var result = await _router.ExecuteAsync(run.RootContext);

                    exec.Output = result.Output;
                    exec.Status = result.IsSuccess ? StepExecutionStatus.Success : StepExecutionStatus.Failed;
                    if (!result.IsSuccess)
                    {
                        exec.Error = exec.Error ?? "AgentResult.IsSuccess=false";
                        run.Status = AgentRunStatus.Failed;
                    }

                    // 可选: 支持动态路由
                    if (!string.IsNullOrWhiteSpace(result.NextAgent))
                    {
                        run.RootContext.Target = result.NextAgent!;
                    }
                }
                catch (Exception ex)
                {
                    exec.Error = ex.Message;
                    exec.Status = StepExecutionStatus.Failed;
                    run.Status = AgentRunStatus.Failed;

                    return;
                }

                run.Status = AgentRunStatus.Completed;
            }
        }

        /// <summary>
        /// 把PlanStep转成Router/Agent能理解的AgentContext输入协议
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="step">The step.</param>
        private static void PrepareContextForStep(AgentContext context, PlanStep step)
        {
            context.State["target"] = step.Agent;
            //统一: 把 instruction 也放进 State 供目标 Agent 使用
            context.State["instruction"] = step.Instruction;

            //反思/对齐，ExpectedOutput 放进 State
            if (!string.IsNullOrWhiteSpace(step.ExpectedOutput))
                context.State["expectedOutput"] = step.ExpectedOutput!;
        }
        private static string BuildStepInput(PlanStep step, string previousOutput)
        {
            if (string.IsNullOrWhiteSpace(previousOutput))
            {
                return step.Instruction;
            }

            return
    $"""
当前步骤说明：
{step.Instruction}

已有上下文信息：
{previousOutput}
""";
        }
    }
}
