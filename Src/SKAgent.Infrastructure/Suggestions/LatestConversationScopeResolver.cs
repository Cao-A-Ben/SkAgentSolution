using Npgsql;
using SKAgent.Core.Suggestions;

namespace SKAgent.Infrastructure.Suggestions;

public sealed class LatestConversationScopeResolver : IConversationScopeResolver
{
    private readonly NpgsqlDataSource _dataSource;

    public LatestConversationScopeResolver(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<string?> ResolveAsync(CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        SELECT conversation_id
        FROM memory_chunks
        GROUP BY conversation_id
        ORDER BY MAX(ts) DESC
        LIMIT 1;
        """;

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is string conversationId && !string.IsNullOrWhiteSpace(conversationId)
            ? conversationId
            : null;
    }
}
