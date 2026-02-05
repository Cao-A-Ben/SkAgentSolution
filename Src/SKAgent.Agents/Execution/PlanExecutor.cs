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
                    // 标准协议写入
                    run.Root.Target = step.Agent;
                    run.Root.Input = step.Instruction;
                    run.Root.ExpectedOutput = step.ExpectedOutput;

                    var result = await _router.ExecuteAsync(run.Root);

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
                        run.Root.Target = result.NextAgent!;
                    }
                }
                catch (Exception ex)
                {
                    exec.Error = ex.Message;
                    exec.Status = StepExecutionStatus.Failed;
                    run.Status = AgentRunStatus.Failed;

                    return;
                }

                run.FinalOutput = string.Join("\n", run.Steps.Select(d => d.Output));
                run.Status = AgentRunStatus.Completed;
            }
        }

    }
}
