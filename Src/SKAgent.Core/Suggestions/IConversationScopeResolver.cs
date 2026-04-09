namespace SKAgent.Core.Suggestions;

public interface IConversationScopeResolver
{
    Task<string?> ResolveAsync(CancellationToken ct = default);
}
