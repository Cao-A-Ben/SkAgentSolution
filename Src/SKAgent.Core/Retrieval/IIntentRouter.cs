namespace SKAgent.Core.Retrieval;

public interface IIntentRouter
{
    Task<IntentRoutingResult> RouteAsync(
        string input,
        IReadOnlyDictionary<string, string>? profile,
        CancellationToken ct = default);
}
