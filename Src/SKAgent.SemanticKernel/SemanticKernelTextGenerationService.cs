using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SKAgent.Core.Modeling;

namespace SKAgent.SemanticKernel;

public sealed class SemanticKernelTextGenerationService : ITextGenerationService
{
    private readonly Kernel _kernel;
    private readonly IModelRouter _modelRouter;

    public SemanticKernelTextGenerationService(Kernel kernel, IModelRouter modelRouter)
    {
        _kernel = kernel;
        _modelRouter = modelRouter;
    }

    public async Task<string> GenerateAsync(TextGenerationRequest request, CancellationToken ct = default)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(request.SystemPrompt);
        history.AddUserMessage(request.UserPrompt);

        var selection = _modelRouter.Select(request.Purpose);
        var settings = new OpenAIPromptExecutionSettings
        {
            ModelId = selection.Model,
            Temperature = request.Temperature,
            TopP = request.TopP
        };

        var message = await chat.GetChatMessageContentAsync(
            history,
            executionSettings: settings,
            kernel: _kernel,
            cancellationToken: ct).ConfigureAwait(false);

        return message.Content ?? string.Empty;
    }
}
