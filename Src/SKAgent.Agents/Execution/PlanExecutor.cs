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


        public async Task<PlanExecutionResult> ExecuteAsync(AgentRunContext context)
        {
           if(context.Plan==null)
                throw new InvalidOperationException("No plan available for execution.");

           context.Status=AgentRunStatus.Executing;

            int stepIndex = 0;

            foreach(var step in context.Plan.Steps)
            {
                stepIndex++;

                //创建Step执行对象 写入上下文
             
            }
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
