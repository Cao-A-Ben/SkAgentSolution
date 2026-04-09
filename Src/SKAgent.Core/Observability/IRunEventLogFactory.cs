namespace SKAgent.Core.Observability;

public sealed record RunEventLogHandle(
    IRunEventSink Sink,
    string Path);

public interface IRunEventLogFactory
{
    RunEventLogHandle CreateDailySuggestionLog(DateOnly date);
}
