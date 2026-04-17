using System.Text.Json;
using SKAgent.Core.Replay;
using SKAgent.Core.Suggestions;

namespace SKAgent.Application.Replay;

public sealed class ReplayQueryService
{
    private readonly IReplayRunStore _replayRunStore;
    private readonly ISuggestionStore _suggestionStore;

    public ReplayQueryService(IReplayRunStore replayRunStore, ISuggestionStore suggestionStore)
    {
        _replayRunStore = replayRunStore;
        _suggestionStore = suggestionStore;
    }

    public async Task<IReadOnlyList<ReplayRunSummary>> ListRunsAsync(int take = 30, CancellationToken ct = default)
    {
        var safeTake = Math.Max(1, take);
        var indexedRuns = (await _replayRunStore.ListRecentAsync(safeTake, ct).ConfigureAwait(false))
            .Select(ProjectSummary)
            .ToList();
        var legacyAgentRuns = await LoadLegacyAgentRunSummariesAsync(indexedRuns, safeTake, ct).ConfigureAwait(false);
        var legacySuggestionRuns = await LoadLegacySuggestionRunSummariesAsync(indexedRuns, safeTake, ct).ConfigureAwait(false);

        return indexedRuns
            .Concat(legacyAgentRuns)
            .Concat(legacySuggestionRuns)
            .OrderByDescending(x => x.StartedAt ?? x.FinishedAt ?? DateTimeOffset.MinValue)
            .Take(safeTake)
            .ToList();
    }

    public async Task<ReplayRunDetail?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        var source = await ResolveRunSourceAsync(runId, ct).ConfigureAwait(false);
        if (source is null)
            return null;

        var events = await ReadEventsAsync(source.Path, ct).ConfigureAwait(false);
        if (events.Count == 0)
            return null;

        return new ReplayRunDetail(
            ProjectSummary(events, source.Kind, source.Suggestion),
            ProjectPrompt(events),
            ProjectSteps(events),
            ProjectMemory(events));
    }

    public async Task<IReadOnlyList<ReplayEventEnvelope>> GetEventsAsync(string runId, CancellationToken ct = default)
    {
        var source = await ResolveRunSourceAsync(runId, ct).ConfigureAwait(false);
        if (source is null)
            return Array.Empty<ReplayEventEnvelope>();

        return await ReadEventsAsync(source.Path, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReplaySuggestionSummary>> ListSuggestionsAsync(int take = 30, CancellationToken ct = default)
    {
        var records = await _suggestionStore.ListRecentAsync(Math.Max(1, take), ct).ConfigureAwait(false);

        return records
            .Select(x => new ReplaySuggestionSummary(
                x.Date.ToString("yyyy-MM-dd"),
                x.Suggestion,
                x.RunId,
                x.ConversationId,
                x.PersonaName,
                x.PromptHash,
                x.ProfileHash,
                x.CreatedAtUtc,
                x.EventLogPath,
                !string.IsNullOrWhiteSpace(x.EventLogPath) && File.Exists(x.EventLogPath)))
            .ToList();
    }

    private async Task<IReadOnlyList<ReplayRunSummary>> LoadLegacySuggestionRunSummariesAsync(
        IReadOnlyList<ReplayRunSummary> indexedRuns,
        int take,
        CancellationToken ct)
    {
        var indexedRunIds = indexedRuns
            .Select(x => x.RunId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var records = await _suggestionStore.ListRecentAsync(Math.Max(1, take), ct).ConfigureAwait(false);
        var result = new List<ReplayRunSummary>(records.Count);

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();
            if (indexedRunIds.Contains(record.RunId))
                continue;

            if (!string.IsNullOrWhiteSpace(record.EventLogPath) && File.Exists(record.EventLogPath))
            {
                var events = await ReadEventsAsync(record.EventLogPath, ct).ConfigureAwait(false);
                if (events.Count > 0)
                {
                    result.Add(ProjectSummary(events, "daily", record));
                    continue;
                }
            }

            result.Add(new ReplayRunSummary(
                record.RunId,
                "daily",
                record.ConversationId,
                record.PersonaName,
                "completed",
                record.CreatedAtUtc,
                record.CreatedAtUtc,
                "Generate one daily suggestion for today.",
                "Generate one daily suggestion for today.",
                Trim(record.Suggestion, 240),
                EventCount: 0));
        }

        return result;
    }

    private async Task<IReadOnlyList<ReplayRunSummary>> LoadLegacyAgentRunSummariesAsync(
        IReadOnlyList<ReplayRunSummary> indexedRuns,
        int take,
        CancellationToken ct)
    {
        var indexedRunIds = indexedRuns
            .Select(x => x.RunId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dir = GetAgentRunDirectory();
        if (!Directory.Exists(dir))
            return Array.Empty<ReplayRunSummary>();

        var files = new DirectoryInfo(dir)
            .EnumerateFiles("*.jsonl", SearchOption.TopDirectoryOnly)
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .Take(Math.Max(take * 3, take))
            .ToList();

        var result = new List<ReplayRunSummary>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var runId = Path.GetFileNameWithoutExtension(file.Name);
            if (indexedRunIds.Contains(runId))
                continue;

            var events = await ReadEventsAsync(file.FullName, ct).ConfigureAwait(false);
            if (events.Count == 0)
                continue;

            result.Add(ProjectSummary(events, "agent", suggestion: null));
        }

        return result;
    }

    private async Task<ReplayRunSource?> ResolveRunSourceAsync(string runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return null;

        var indexedRun = await _replayRunStore.GetAsync(runId, ct).ConfigureAwait(false);
        if (indexedRun is not null && !string.IsNullOrWhiteSpace(indexedRun.EventLogPath) && File.Exists(indexedRun.EventLogPath))
            return new ReplayRunSource(indexedRun.Kind, indexedRun.EventLogPath, null);

        var agentPath = GetAgentRunPath(runId);
        if (File.Exists(agentPath))
            return new ReplayRunSource("agent", agentPath, null);

        var suggestion = await _suggestionStore.GetByRunIdAsync(runId, ct).ConfigureAwait(false);
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.EventLogPath))
            return null;

        return File.Exists(suggestion.EventLogPath)
            ? new ReplayRunSource("daily", suggestion.EventLogPath, suggestion)
            : null;
    }

    private static async Task<IReadOnlyList<ReplayEventEnvelope>> ReadEventsAsync(string path, CancellationToken ct)
    {
        var result = new List<ReplayEventEnvelope>();
        if (!File.Exists(path))
            return result;

        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            var runId = GetString(root, "runId") ?? string.Empty;
            var seq = GetInt64(root, "seq") ?? 0L;
            var timestamp = GetTimestamp(root, "ts") ?? DateTimeOffset.MinValue;
            var type = GetString(root, "type") ?? string.Empty;
            var payload = root.TryGetProperty("payload", out var payloadElement)
                ? payloadElement.Clone()
                : JsonDocument.Parse("{}").RootElement.Clone();

            result.Add(new ReplayEventEnvelope(runId, seq, timestamp, type, payload));
        }

        result.Sort((a, b) => a.Seq.CompareTo(b.Seq));
        return result;
    }

    private static ReplayRunSummary ProjectSummary(
        IReadOnlyList<ReplayEventEnvelope> events,
        string kind,
        SuggestionRecord? suggestion)
    {
        var first = events[0];
        var summary = new ReplayRunSummary(
            first.RunId,
            kind,
            ConversationId: FindConversationId(events) ?? suggestion?.ConversationId,
            PersonaName: FindPersonaName(events) ?? suggestion?.PersonaName,
            Status: FindStatus(events),
            StartedAt: first.Timestamp,
            FinishedAt: FindFinishedAt(events),
            Goal: FindPlanGoal(events),
            InputPreview: FindInputPreview(events),
            FinalOutputPreview: FindFinalOutputPreview(events) ?? (suggestion is null ? null : Trim(suggestion.Suggestion, 240)),
            EventCount: events.Count);

        return summary;
    }

    private static ReplayRunSummary ProjectSummary(ReplayRunRecord record)
        => new(
            record.RunId,
            record.Kind,
            record.ConversationId,
            record.PersonaName,
            record.Status,
            record.StartedAtUtc,
            record.FinishedAtUtc,
            record.Goal,
            record.InputPreview,
            record.FinalOutputPreview,
            EventCount: 0);

    private static string GetAgentRunDirectory()
        => Path.Combine(AppContext.BaseDirectory, "data", "replay", "runs");

    private static string GetAgentRunPath(string runId)
        => Path.Combine(GetAgentRunDirectory(), $"{runId}.jsonl");

    private static ReplayPromptSummary? ProjectPrompt(IReadOnlyList<ReplayEventEnvelope> events)
    {
        var promptEvent = events.LastOrDefault(x => string.Equals(x.Type, "prompt_composed", StringComparison.OrdinalIgnoreCase));
        if (promptEvent is null)
            return null;

        return new ReplayPromptSummary(
            GetString(promptEvent.Payload, "target"),
            GetString(promptEvent.Payload, "hash"),
            GetInt32(promptEvent.Payload, "charBudget"),
            GetStringArray(promptEvent.Payload, "layersUsed"),
            GetInt32(promptEvent.Payload, "systemChars"),
            GetInt32(promptEvent.Payload, "userChars"),
            GetString(promptEvent.Payload, "systemText"),
            GetString(promptEvent.Payload, "userText"));
    }

    private static IReadOnlyList<ReplayStepSummary> ProjectSteps(IReadOnlyList<ReplayEventEnvelope> events)
    {
        var steps = new Dictionary<int, ReplayStepState>();

        foreach (var evt in events)
        {
            if (string.Equals(evt.Type, "plan_created", StringComparison.OrdinalIgnoreCase)
                && evt.Payload.TryGetProperty("steps", out var planSteps)
                && planSteps.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in planSteps.EnumerateArray())
                {
                    var order = GetInt32(item, "order");
                    if (!order.HasValue)
                        continue;

                    steps[order.Value] = new ReplayStepState(
                        order.Value,
                        GetString(item, "kind"),
                        GetString(item, "target"),
                        "planned",
                        null,
                        null);
                }
            }

            if (!TryGetStepOrder(evt.Payload, out var stepOrder))
                continue;

            steps.TryGetValue(stepOrder, out var current);
            current ??= new ReplayStepState(stepOrder, null, null, "planned", null, null);

            switch (evt.Type)
            {
                case "step_started":
                    current = current with
                    {
                        Kind = GetString(evt.Payload, "kind") ?? current.Kind,
                        Target = GetString(evt.Payload, "target") ?? current.Target,
                        Status = "running"
                    };
                    break;
                case "step_completed":
                    current = current with
                    {
                        Status = GetBool(evt.Payload, "success") == false ? "failed" : "completed",
                        OutputPreview = GetString(evt.Payload, "outputPreview") ?? current.OutputPreview
                    };
                    break;
                case "step_failed":
                    current = current with
                    {
                        Status = "failed",
                        Error = GetString(evt.Payload, "error") ?? current.Error
                    };
                    break;
            }

            steps[stepOrder] = current;
        }

        return steps.Values
            .OrderBy(x => x.Order)
            .Select(x => new ReplayStepSummary(x.Order, x.Kind, x.Target, x.Status, x.OutputPreview, x.Error))
            .ToList();
    }

    private static ReplayMemorySummary? ProjectMemory(IReadOnlyList<ReplayEventEnvelope> events)
    {
        var recallEvent = events.LastOrDefault(x => string.Equals(x.Type, "recall_summary_built", StringComparison.OrdinalIgnoreCase));
        var fusedEvent = events.LastOrDefault(x => string.Equals(x.Type, "memory_fused", StringComparison.OrdinalIgnoreCase));
        var vectorEvent = events.LastOrDefault(x => string.Equals(x.Type, "vector_query_executed", StringComparison.OrdinalIgnoreCase));

        var layers = events
            .Where(x => string.Equals(x.Type, "memory_layer_included", StringComparison.OrdinalIgnoreCase))
            .Select(x => new ReplayMemoryLayerSummary(
                GetString(x.Payload, "layer") ?? "unknown",
                GetInt32(x.Payload, "countBefore"),
                GetInt32(x.Payload, "countAfter"),
                GetInt32(x.Payload, "budgetChars"),
                GetString(x.Payload, "truncateReason")))
            .ToList();

        var byRouteCounts = fusedEvent is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : GetIntDictionary(fusedEvent.Payload, "byRouteCounts");

        var memory = new ReplayMemorySummary(
            RecallSource: recallEvent is null ? null : GetString(recallEvent.Payload, "source"),
            RecallPreview: recallEvent is null ? null : GetString(recallEvent.Payload, "preview"),
            ByRouteCounts: byRouteCounts,
            TotalItems: fusedEvent is null ? null : GetInt32(fusedEvent.Payload, "totalItems"),
            BudgetUsed: fusedEvent is null ? null : GetInt32(fusedEvent.Payload, "budgetUsed"),
            ConflictsResolved: fusedEvent is null ? null : GetInt32(fusedEvent.Payload, "conflictsResolved"),
            Layers: layers,
            VectorTopK: vectorEvent is null ? null : GetInt32(vectorEvent.Payload, "topK"),
            VectorLatencyMs: vectorEvent is null ? null : GetInt32(vectorEvent.Payload, "latencyMs"),
            VectorScoreMin: vectorEvent is null ? null : GetNestedDouble(vectorEvent.Payload, "scoreRange", "min"),
            VectorScoreMax: vectorEvent is null ? null : GetNestedDouble(vectorEvent.Payload, "scoreRange", "max"));

        var hasContent = memory.RecallSource is not null
            || memory.ByRouteCounts.Count > 0
            || memory.Layers.Count > 0
            || memory.VectorTopK.HasValue;

        return hasContent ? memory : null;
    }

    private static string? FindConversationId(IEnumerable<ReplayEventEnvelope> events)
        => events
            .Select(evt => GetString(evt.Payload, "conversationId"))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string? FindPersonaName(IEnumerable<ReplayEventEnvelope> events)
        => events
            .Select(evt => GetString(evt.Payload, "personaName"))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string FindStatus(IEnumerable<ReplayEventEnvelope> events)
    {
        if (events.Any(x => string.Equals(x.Type, "daily_job_failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Type, "run_failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "failed";
        }

        if (events.Any(x => string.Equals(x.Type, "daily_job_finished", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Type, "run_completed", StringComparison.OrdinalIgnoreCase)))
        {
            return "completed";
        }

        return "running";
    }

    private static DateTimeOffset? FindFinishedAt(IEnumerable<ReplayEventEnvelope> events)
        => events
            .Where(x => string.Equals(x.Type, "daily_job_finished", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Type, "daily_job_failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Type, "run_completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Type, "run_failed", StringComparison.OrdinalIgnoreCase))
            .Select(x => (DateTimeOffset?)x.Timestamp)
            .LastOrDefault();

    private static string? FindPlanGoal(IEnumerable<ReplayEventEnvelope> events)
        => events
            .Where(x => string.Equals(x.Type, "plan_created", StringComparison.OrdinalIgnoreCase))
            .Select(x => GetString(x.Payload, "goal"))
            .LastOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string? FindInputPreview(IEnumerable<ReplayEventEnvelope> events)
    {
        var runStarted = events
            .Where(x => string.Equals(x.Type, "run_started", StringComparison.OrdinalIgnoreCase))
            .Select(x => GetString(x.Payload, "input"))
            .LastOrDefault(x => !string.IsNullOrWhiteSpace(x));

        if (!string.IsNullOrWhiteSpace(runStarted))
            return Trim(runStarted, 240);

        var dailyDate = events
            .Where(x => string.Equals(x.Type, "daily_job_started", StringComparison.OrdinalIgnoreCase))
            .Select(x => GetString(x.Payload, "date"))
            .LastOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return string.IsNullOrWhiteSpace(dailyDate)
            ? null
            : $"Daily suggestion for {dailyDate}";
    }

    private static string? FindFinalOutputPreview(IEnumerable<ReplayEventEnvelope> events)
        => events
            .Where(x => string.Equals(x.Type, "run_completed", StringComparison.OrdinalIgnoreCase))
            .Select(x => GetString(x.Payload, "finalOutput"))
            .LastOrDefault(x => !string.IsNullOrWhiteSpace(x)) is { } output
                ? Trim(output, 240)
                : null;

    private static bool TryGetStepOrder(JsonElement payload, out int order)
    {
        order = 0;
        var value = GetInt32(payload, "order");
        if (!value.HasValue)
            return false;

        order = value.Value;
        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    private static long? GetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;

        return value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static DateTimeOffset? GetTimestamp(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), out var timestamp))
        {
            return timestamp;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixMilliseconds))
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!)
            .ToList();
    }

    private static IReadOnlyDictionary<string, int> GetIntDictionary(JsonElement element, string propertyName)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var number))
                dict[property.Name] = number;
        }

        return dict;
    }

    private static double? GetNestedDouble(JsonElement element, string propertyName, string nestedPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var nested) || nested.ValueKind != JsonValueKind.Object)
            return null;

        if (!nested.TryGetProperty(nestedPropertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            return number;

        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    private static string Trim(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxChars)
            return value;

        return value[..maxChars] + "...";
    }

    private sealed record ReplayRunSource(string Kind, string Path, SuggestionRecord? Suggestion);

    private sealed record ReplayStepState(
        int Order,
        string? Kind,
        string? Target,
        string Status,
        string? OutputPreview,
        string? Error);
}
