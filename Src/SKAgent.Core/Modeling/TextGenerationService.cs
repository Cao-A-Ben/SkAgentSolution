namespace SKAgent.Core.Modeling;

public sealed record TextGenerationRequest(
    string SystemPrompt,
    string UserPrompt,
    ModelPurpose Purpose,
    double Temperature = 0.4,
    double TopP = 0.9);

public interface ITextGenerationService
{
    Task<string> GenerateAsync(TextGenerationRequest request, CancellationToken ct = default);
}
