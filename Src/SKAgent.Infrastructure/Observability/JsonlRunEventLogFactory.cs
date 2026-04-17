using SKAgent.Core.Observability;

namespace SKAgent.Infrastructure.Observability;

public sealed class JsonlRunEventLogFactory : IRunEventLogFactory
{
    public RunEventLogHandle CreateAgentRunLog(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        var dir = Path.Combine(AppContext.BaseDirectory, "data", "replay", "runs");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{runId}.jsonl");
        return new RunEventLogHandle(new JsonlRunEventSink(path), path);
    }

    public RunEventLogHandle CreateDailySuggestionLog(DateOnly date)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "daily-suggestions", date.ToString("yyyyMMdd"));
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.jsonl");
        return new RunEventLogHandle(new JsonlRunEventSink(path), path);
    }
}
