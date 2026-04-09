using SKAgent.Core.Suggestions;

namespace SKAgent.Infrastructure.Suggestions;

public sealed class NullConversationScopeResolver : IConversationScopeResolver
{
    public Task<string?> ResolveAsync(CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
