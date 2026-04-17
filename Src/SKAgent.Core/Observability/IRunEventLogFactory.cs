namespace SKAgent.Core.Observability;

public sealed record RunEventLogHandle(
    IRunEventSink Sink,
    string Path);

public interface IRunEventLogFactory
{
    RunEventLogHandle CreateAgentRunLog(string runId);

    RunEventLogHandle CreateDailySuggestionLog(DateOnly date);
}
