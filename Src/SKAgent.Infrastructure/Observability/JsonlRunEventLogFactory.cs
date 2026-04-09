using SKAgent.Core.Observability;

namespace SKAgent.Infrastructure.Observability;

public sealed class JsonlRunEventLogFactory : IRunEventLogFactory
{
    public RunEventLogHandle CreateDailySuggestionLog(DateOnly date)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "daily-suggestions", date.ToString("yyyyMMdd"));
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.jsonl");
        return new RunEventLogHandle(new JsonlRunEventSink(path), path);
    }
}
