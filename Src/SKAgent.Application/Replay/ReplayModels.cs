using System.Text.Json;

namespace SKAgent.Application.Replay;

public sealed record ReplayEventEnvelope(
    string RunId,
    long Seq,
    DateTimeOffset Timestamp,
    string Type,
    JsonElement Payload);

public sealed record ReplayRunSummary(
    string RunId,
    string Kind,
    string? ConversationId,
    string? PersonaName,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Goal,
    string? InputPreview,
    string? FinalOutputPreview,
    int EventCount);

public sealed record ReplayPromptSummary(
    string? Target,
    string? Hash,
    int? CharBudget,
    IReadOnlyList<string> LayersUsed,
    int? SystemChars,
    int? UserChars,
    string? SystemText,
    string? UserText);

public sealed record ReplayStepSummary(
    int Order,
    string? Kind,
    string? Target,
    string Status,
    string? OutputPreview,
    string? Error);

public sealed record ReplayMemoryLayerSummary(
    string Layer,
    int? CountBefore,
    int? CountAfter,
    int? BudgetChars,
    string? TruncateReason);

public sealed record ReplayMemorySummary(
    string? RecallSource,
    string? RecallPreview,
    IReadOnlyDictionary<string, int> ByRouteCounts,
    int? TotalItems,
    int? BudgetUsed,
    int? ConflictsResolved,
    IReadOnlyList<ReplayMemoryLayerSummary> Layers,
    int? VectorTopK,
    int? VectorLatencyMs,
    double? VectorScoreMin,
    double? VectorScoreMax);

public sealed record ReplayRunDetail(
    ReplayRunSummary Summary,
    ReplayPromptSummary? Prompt,
    IReadOnlyList<ReplayStepSummary> Steps,
    ReplayMemorySummary? Memory);

public sealed record ReplaySuggestionSummary(
    string Date,
    string Suggestion,
    string RunId,
    string ConversationId,
    string PersonaName,
    string PromptHash,
    string ProfileHash,
    DateTimeOffset CreatedAtUtc,
    string? EventLogPath,
    bool ReplayAvailable);
