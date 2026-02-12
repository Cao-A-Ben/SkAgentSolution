using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SKAgent.Agents.Chat;
using SKAgent.Core.Agent;

namespace SKAgent.Agents
{
    public class SKChatAgent : IAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatContextComposer _composer;

        public string Name => "chat";

        public SKChatAgent(Kernel kernel,  IChatContextComposer composer)
        {
            _kernel = kernel;
            _composer = composer;
        }

        public async Task<AgentResult> ExecuteAsync(AgentContext context)
        {

            var chat=_kernel.GetRequiredService<IChatCompletionService>();
            var composed = _composer.Compose(context);

            var history = new ChatHistory();
            history.AddSystemMessage(composed.SystemMessage);
            history.AddUserMessage(composed.UserMessage);

            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3,
                TopP = 0.9
            };

            var msg = await chat.GetChatMessageContentAsync(
                history,
                executionSettings: settings,
                kernel: _kernel,
                context.CancellationToken);

            return new AgentResult
            {
                Output = msg.Content ?? string.Empty,
                IsSuccess = true
            };

        }
    }
}
