namespace SKAgent.Core.Suggestions;

public sealed record SuggestionRecord(
    DateOnly Date,
    string Suggestion,
    string RunId,
    string ConversationId,
    string PersonaName,
    string ProfileHash,
    string PromptHash,
    DateTimeOffset CreatedAtUtc,
    string? EventLogPath = null);
