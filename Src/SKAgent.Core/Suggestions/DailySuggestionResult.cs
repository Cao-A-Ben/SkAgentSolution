namespace SKAgent.Core.Suggestions;

public sealed record DailySuggestionResult(
    SuggestionRecord Record,
    bool Created);
