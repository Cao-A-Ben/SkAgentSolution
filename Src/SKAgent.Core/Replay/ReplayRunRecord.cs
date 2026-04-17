namespace SKAgent.Core.Replay;

public sealed record ReplayRunRecord(
    string RunId,
    string Kind,
    string ConversationId,
    string Status,
    string? PersonaName,
    string? Goal,
    string? InputPreview,
    string? FinalOutputPreview,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string EventLogPath);
