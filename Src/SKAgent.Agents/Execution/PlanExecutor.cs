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


        public async Task<PlanExecutionResult> ExecuteAsync(AgentRunContext context, CancellationToken ct = default)
        {
            if (context.Plan == null)
                throw new InvalidOperationException("No plan available for execution.");

            context.Status = AgentRunStatus.Executing;
            //按步骤顺序执行
            var steps = context.Plan.Steps.OrderBy(d => d.Order).ToList();

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                //创建Step执行对象 写入上下文
                var exec = new PlanStepExecution
                {
                    Order = step.Order,
                    Agent = step.Agent,
                    Input = step.Instruction,
                    Status = StepExecutionStatus.Running
                };
                context.StepExecutions.Add(exec);

                try
                {
                    var output=await _router.ExecuteAsync(step.Agent, BuildStepInput(step, GetPreviousOutput(context)), ct);
                }
            }
        }


        private async Task<string> ExecuteByRouterAsync(PlanStep step,CancellationToken ct)
        {

            _router.ExecuteAsync(step.Agent, step.Instruction, ct);
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
