using System.Text.Json;

namespace SKAgent.Host.Contracts.Replay;

public sealed class ReplayEventResponse
{
    public string RunId { get; init; } = string.Empty;

    public long Seq { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public string Type { get; init; } = string.Empty;

    public JsonElement Payload { get; init; }
}

public sealed class ReplayRunSummaryResponse
{
    public string RunId { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string? ConversationId { get; init; }

    public string? PersonaName { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? Goal { get; init; }

    public string? InputPreview { get; init; }

    public string? FinalOutputPreview { get; init; }

    public int EventCount { get; init; }
}

public sealed class ReplayPromptResponse
{
    public string? Target { get; init; }

    public string? Hash { get; init; }

    public int? CharBudget { get; init; }

    public IReadOnlyList<string> LayersUsed { get; init; } = Array.Empty<string>();

    public int? SystemChars { get; init; }

    public int? UserChars { get; init; }

    public string? SystemText { get; init; }

    public string? UserText { get; init; }
}

public sealed class ReplayStepResponse
{
    public int Order { get; init; }

    public string? Kind { get; init; }

    public string? Target { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? OutputPreview { get; init; }

    public string? Error { get; init; }
}

public sealed class ReplayMemoryLayerResponse
{
    public string Layer { get; init; } = string.Empty;

    public int? CountBefore { get; init; }

    public int? CountAfter { get; init; }

    public int? BudgetChars { get; init; }

    public string? TruncateReason { get; init; }
}

public sealed class ReplayMemoryResponse
{
    public string? RecallSource { get; init; }

    public string? RecallPreview { get; init; }

    public IReadOnlyDictionary<string, int> ByRouteCounts { get; init; } = new Dictionary<string, int>();

    public int? TotalItems { get; init; }

    public int? BudgetUsed { get; init; }

    public int? ConflictsResolved { get; init; }

    public IReadOnlyList<ReplayMemoryLayerResponse> Layers { get; init; } = Array.Empty<ReplayMemoryLayerResponse>();

    public int? VectorTopK { get; init; }

    public int? VectorLatencyMs { get; init; }

    public double? VectorScoreMin { get; init; }

    public double? VectorScoreMax { get; init; }
}

public sealed class ReplayRunDetailResponse
{
    public ReplayRunSummaryResponse Summary { get; init; } = new();

    public ReplayPromptResponse? Prompt { get; init; }

    public IReadOnlyList<ReplayStepResponse> Steps { get; init; } = Array.Empty<ReplayStepResponse>();

    public ReplayMemoryResponse? Memory { get; init; }
}

public sealed class ReplaySuggestionResponse
{
    public string Date { get; init; } = string.Empty;

    public string Suggestion { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string ConversationId { get; init; } = string.Empty;

    public string PersonaName { get; init; } = string.Empty;

    public string PromptHash { get; init; } = string.Empty;

    public string ProfileHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public string? EventLogPath { get; init; }

    public bool ReplayAvailable { get; init; }
}
