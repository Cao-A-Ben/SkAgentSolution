using System.Text.Json;
using Npgsql;
using SKAgent.Core.Memory.ShortTerm;

namespace SKAgent.Infrastructure.Memory.ShortTerm;

public sealed class PgRecentConversationHistory : IRecentConversationHistory
{
    private readonly NpgsqlDataSource _dataSource;

    public PgRecentConversationHistory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<TurnRecord>> GetRecentAsync(
        string conversationId,
        int take,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || take <= 0)
            return [];

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        SELECT run_id,
               ts,
               content,
               metadata::text
        FROM memory_chunks
        WHERE conversation_id = @conversation_id
          AND (metadata->>'source' = 'user_input' OR metadata->>'source' = 'assistant_output')
        ORDER BY ts DESC
        LIMIT @limit;
        """;

        cmd.Parameters.AddWithValue("conversation_id", conversationId);
        cmd.Parameters.AddWithValue("limit", Math.Max(8, take * 6));

        var rows = new List<Row>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new Row(
                RunId: reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Ts: reader.GetFieldValue<DateTimeOffset>(1),
                Content: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Metadata: ParseMetadata(reader.IsDBNull(3) ? "{}" : reader.GetString(3))));
        }

        var turns = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.RunId))
            .GroupBy(r => r.RunId)
            .Select(g => new TurnRecord
            {
                At = g.Max(x => x.Ts),
                UserInput = JoinContent(g, "user_input", "[user]"),
                AssistantOutput = JoinContent(g, "assistant_output", "[assistant]"),
                Goal = string.Empty,
                Steps = Array.Empty<StepRecord>()
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.UserInput) || !string.IsNullOrWhiteSpace(t.AssistantOutput))
            .OrderByDescending(t => t.At)
            .Take(take)
            .ToList();

        return turns;
    }

    private static string JoinContent(IEnumerable<Row> rows, string source, string prefix)
    {
        var parts = rows
            .Where(r => r.Metadata.TryGetValue("source", out var s) && string.Equals(s, source, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Ts)
            .Select(r => StripPrefix(SingleLine(r.Content), prefix))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        return string.Join("", parts).Trim();
    }

    private static Dictionary<string, string> ParseMetadata(string metadataJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string StripPrefix(string text, string prefix)
    {
        if ((text ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return text[prefix.Length..].Trim();
        return text ?? string.Empty;
    }

    private static string SingleLine(string text)
        => (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

    private sealed record Row(
        string RunId,
        DateTimeOffset Ts,
        string Content,
        IReadOnlyDictionary<string, string> Metadata);
}
