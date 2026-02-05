using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SemanticKernel;
using SKAgent.Core.Agent;

namespace SKAgent.Agents
{
    public class SKChatAgent : IAgent
    {
        private readonly Kernel _kernel;

        public string Name => "chat";

        public SKChatAgent(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<AgentResult> ExecuteAsync(AgentContext context)
        {

            var result = await _kernel.InvokePromptAsync(context.Input);

            return new AgentResult
            {
                Output = result.GetValue<string>() ?? string.Empty,
                IsSuccess = true
            };

        }
    }
}
