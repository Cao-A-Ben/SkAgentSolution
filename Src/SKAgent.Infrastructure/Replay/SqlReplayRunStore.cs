using Npgsql;
using SKAgent.Core.Replay;

namespace SKAgent.Infrastructure.Replay;

public sealed class SqlReplayRunStore : IReplayRunStore
{
    private readonly NpgsqlDataSource _dataSource;

    public SqlReplayRunStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task SaveAsync(ReplayRunRecord record, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        INSERT INTO replay_runs (
            run_id,
            run_kind,
            conversation_id,
            status,
            persona_name,
            goal,
            input_preview,
            final_output_preview,
            started_at,
            finished_at,
            event_log_path)
        VALUES (
            @run_id,
            @run_kind,
            @conversation_id,
            @status,
            @persona_name,
            @goal,
            @input_preview,
            @final_output_preview,
            @started_at,
            @finished_at,
            @event_log_path)
        ON CONFLICT (run_id)
        DO UPDATE SET
            run_kind = EXCLUDED.run_kind,
            conversation_id = EXCLUDED.conversation_id,
            status = EXCLUDED.status,
            persona_name = EXCLUDED.persona_name,
            goal = EXCLUDED.goal,
            input_preview = EXCLUDED.input_preview,
            final_output_preview = EXCLUDED.final_output_preview,
            started_at = EXCLUDED.started_at,
            finished_at = EXCLUDED.finished_at,
            event_log_path = EXCLUDED.event_log_path;
        """;

        Bind(cmd, record);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<ReplayRunRecord?> GetAsync(string runId, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        SELECT run_id,
               run_kind,
               conversation_id,
               status,
               persona_name,
               goal,
               input_preview,
               final_output_preview,
               started_at,
               finished_at,
               event_log_path
        FROM replay_runs
        WHERE run_id = @run_id
        LIMIT 1;
        """;

        cmd.Parameters.AddWithValue("run_id", runId);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return Map(reader);
    }

    public async Task<IReadOnlyList<ReplayRunRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
        """
        SELECT run_id,
               run_kind,
               conversation_id,
               status,
               persona_name,
               goal,
               input_preview,
               final_output_preview,
               started_at,
               finished_at,
               event_log_path
        FROM replay_runs
        ORDER BY started_at DESC, finished_at DESC NULLS LAST
        LIMIT @take;
        """;

        cmd.Parameters.AddWithValue("take", Math.Max(1, take));
        var items = new List<ReplayRunRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    private static void Bind(NpgsqlCommand cmd, ReplayRunRecord record)
    {
        cmd.Parameters.AddWithValue("run_id", record.RunId);
        cmd.Parameters.AddWithValue("run_kind", record.Kind);
        cmd.Parameters.AddWithValue("conversation_id", record.ConversationId);
        cmd.Parameters.AddWithValue("status", record.Status);
        cmd.Parameters.AddWithValue("persona_name", (object?)record.PersonaName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("goal", (object?)record.Goal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("input_preview", (object?)record.InputPreview ?? DBNull.Value);
        cmd.Parameters.AddWithValue("final_output_preview", (object?)record.FinalOutputPreview ?? DBNull.Value);
        cmd.Parameters.AddWithValue("started_at", record.StartedAtUtc);
        cmd.Parameters.AddWithValue("finished_at", (object?)record.FinishedAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("event_log_path", record.EventLogPath);
    }

    private static ReplayRunRecord Map(NpgsqlDataReader reader)
        => new(
            RunId: reader.GetString(0),
            Kind: reader.GetString(1),
            ConversationId: reader.GetString(2),
            Status: reader.GetString(3),
            PersonaName: reader.IsDBNull(4) ? null : reader.GetString(4),
            Goal: reader.IsDBNull(5) ? null : reader.GetString(5),
            InputPreview: reader.IsDBNull(6) ? null : reader.GetString(6),
            FinalOutputPreview: reader.IsDBNull(7) ? null : reader.GetString(7),
            StartedAtUtc: reader.GetFieldValue<DateTimeOffset>(8),
            FinishedAtUtc: reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
            EventLogPath: reader.GetString(10));
}
