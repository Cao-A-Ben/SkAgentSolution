using Npgsql;
using SKAgent.Core.Memory.Vector;

namespace SKAgent.Infrastructure.Memory.Vector;

public sealed class PgVectorStore : IVectorStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PgVectorStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<VectorUpsertResult> UpsertAsync(VectorRecord record, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText =
        """
        INSERT INTO memory_chunks
            (chunk_id, conversation_id, run_id, persona, ts, content, content_hash, embedding, metadata)
        VALUES
            (@chunk_id, @conversation_id, @run_id, @persona, @ts, @content, @content_hash, @embedding, @metadata::jsonb)
        ON CONFLICT (content_hash) DO NOTHING;
        """;

        cmd.Parameters.AddWithValue("chunk_id", record.ChunkId);
        cmd.Parameters.AddWithValue("conversation_id", record.ConversationId);
        cmd.Parameters.AddWithValue("run_id", record.RunId);
        cmd.Parameters.AddWithValue("persona", record.Persona);
        cmd.Parameters.AddWithValue("ts", record.Ts);
        cmd.Parameters.AddWithValue("content", record.Content);
        cmd.Parameters.AddWithValue("content_hash", record.ContentHash);
        cmd.Parameters.AddWithValue("embedding", new Pgvector.Vector(record.Embedding));
        cmd.Parameters.AddWithValue("metadata", record.MetadataJson ?? "{}");

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return new VectorUpsertResult(Inserted: affected > 0);
    }

    public async Task<IReadOnlyList<VectorHit>> QueryAsync(VectorQuery query, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText =
        """
        SELECT chunk_id,
               content,
               content_hash,
               metadata::text,
               ts,
               embedding <-> @embedding AS distance
        FROM memory_chunks
        WHERE (@conversation_id IS NULL OR conversation_id = @conversation_id)
        ORDER BY embedding <-> @embedding
        LIMIT @topk;
        """;

        cmd.Parameters.AddWithValue("embedding", new Pgvector.Vector(query.Embedding));
        cmd.Parameters.AddWithValue("conversation_id", string.IsNullOrWhiteSpace(query.ConversationId) ? DBNull.Value : query.ConversationId);
        cmd.Parameters.AddWithValue("topk", query.TopK);

        var list = new List<VectorHit>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new VectorHit
            {
                ChunkId = reader.GetGuid(0),
                Content = reader.GetString(1),
                ContentHash = reader.GetString(2),
                MetadataJson = reader.IsDBNull(3) ? "{}" : reader.GetString(3),
                Ts = reader.GetFieldValue<DateTimeOffset>(4),
                Distance = reader.GetFloat(5)
            });
        }

        return list;
    }
}
