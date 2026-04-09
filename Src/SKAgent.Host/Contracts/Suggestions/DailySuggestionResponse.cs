namespace SKAgent.Host.Contracts.Suggestions;

public sealed class DailySuggestionResponse
{
    public string Date { get; init; } = string.Empty;

    public string Suggestion { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public string PersonaName { get; init; } = string.Empty;

    public string PromptHash { get; init; } = string.Empty;

    public string ProfileHash { get; init; } = string.Empty;

    public string? EventLogPath { get; init; }

    public bool Created { get; init; }
}
