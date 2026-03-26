namespace SKAgent.Core.Retrieval;

public interface IQueryRewriter
{
    Task<IReadOnlyList<string>> RewriteAsync(
        string input,
        RetrievalIntent intents,
        CancellationToken ct = default);
}
