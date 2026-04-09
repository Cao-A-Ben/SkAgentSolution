using Npgsql;
using SKAgent.Core.Suggestions;

namespace SKAgent.Infrastructure.Suggestions;

public sealed class SqlSuggestionStore : ISuggestionStore
{
    private readonly NpgsqlDataSource _dataSource;

    public SqlSuggestionStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<SuggestionRecord?> GetAsync(DateOnly date, string personaName, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        SELECT suggestion_date,
               suggestion,
               run_id,
               conversation_id,
               persona_name,
               profile_hash,
               prompt_hash,
               created_at,
               event_log_path
        FROM daily_suggestions
        WHERE suggestion_date = @suggestion_date
          AND persona_name = @persona_name
        LIMIT 1;
        """;

        cmd.Parameters.AddWithValue("suggestion_date", date);
        cmd.Parameters.AddWithValue("persona_name", personaName);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return Map(reader);
    }

    public async Task SaveAsync(SuggestionRecord record, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        INSERT INTO daily_suggestions (
            suggestion_date,
            suggestion,
            run_id,
            conversation_id,
            persona_name,
            profile_hash,
            prompt_hash,
            created_at,
            event_log_path)
        VALUES (
            @suggestion_date,
            @suggestion,
            @run_id,
            @conversation_id,
            @persona_name,
            @profile_hash,
            @prompt_hash,
            @created_at,
            @event_log_path)
        ON CONFLICT (suggestion_date, persona_name)
        DO UPDATE SET
            suggestion = EXCLUDED.suggestion,
            run_id = EXCLUDED.run_id,
            conversation_id = EXCLUDED.conversation_id,
            profile_hash = EXCLUDED.profile_hash,
            prompt_hash = EXCLUDED.prompt_hash,
            created_at = EXCLUDED.created_at,
            event_log_path = EXCLUDED.event_log_path;
        """;

        Bind(cmd, record);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        SELECT suggestion_date,
               suggestion,
               run_id,
               conversation_id,
               persona_name,
               profile_hash,
               prompt_hash,
               created_at,
               event_log_path
        FROM daily_suggestions
        ORDER BY suggestion_date DESC, created_at DESC
        LIMIT @take;
        """;

        cmd.Parameters.AddWithValue("take", Math.Max(1, take));

        var records = new List<SuggestionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            records.Add(Map(reader));
        }

        return records;
    }

    private static void Bind(NpgsqlCommand cmd, SuggestionRecord record)
    {
        cmd.Parameters.AddWithValue("suggestion_date", record.Date);
        cmd.Parameters.AddWithValue("suggestion", record.Suggestion);
        cmd.Parameters.AddWithValue("run_id", record.RunId);
        cmd.Parameters.AddWithValue("conversation_id", record.ConversationId);
        cmd.Parameters.AddWithValue("persona_name", record.PersonaName);
        cmd.Parameters.AddWithValue("profile_hash", record.ProfileHash);
        cmd.Parameters.AddWithValue("prompt_hash", record.PromptHash);
        cmd.Parameters.AddWithValue("created_at", record.CreatedAtUtc);
        cmd.Parameters.AddWithValue("event_log_path", (object?)record.EventLogPath ?? DBNull.Value);
    }

    private static SuggestionRecord Map(NpgsqlDataReader reader)
        => new(
            Date: reader.GetFieldValue<DateOnly>(0),
            Suggestion: reader.GetString(1),
            RunId: reader.GetString(2),
            ConversationId: reader.GetString(3),
            PersonaName: reader.GetString(4),
            ProfileHash: reader.GetString(5),
            PromptHash: reader.GetString(6),
            CreatedAtUtc: reader.GetFieldValue<DateTimeOffset>(7),
            EventLogPath: reader.IsDBNull(8) ? null : reader.GetString(8));
}
