namespace SKAgent.Host.Contracts.Suggestions;

public sealed class DailySuggestionRunRequest
{
    public string? Date { get; init; }

    public string? PersonaName { get; init; }

    public string? ConversationId { get; init; }
}
