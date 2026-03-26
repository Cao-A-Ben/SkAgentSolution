using System.Security.Cryptography;
using System.Text;
using SKAgent.Core.Embedding;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.Vector;

namespace SKAgent.Infrastructure.Memory.LongTerm;

public sealed class PgLongTermMemory : ILongTermMemory
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embeddingProvider;

    public PgLongTermMemory(
        IVectorStore vectorStore,
        IEmbeddingProvider embeddingProvider)
    {
        _vectorStore = vectorStore;
        _embeddingProvider = embeddingProvider;
    }

    public async Task<IReadOnlyList<MemoryItem>> QueryAsync(MemoryQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
            return [];

        var embedding = await _embeddingProvider.CreateEmbeddingAsync(query.Text, ct);
        var hits = await _vectorStore.QueryAsync(new VectorQuery
        {
            Embedding = embedding,
            TopK = query.TopK,
            ConversationId = query.ConversationId
        }, ct);

        var result = hits.Select(h => new MemoryItem(
            Id: $"lt:{h.ChunkId:N}",
            Layer: MemoryLayer.LongTerm,
            Content: h.Content,
            At: h.Ts,
            Score: 1d / (1d + Math.Max(0.0001f, h.Distance)),
            Metadata: new Dictionary<string, string>
            {
                ["route"] = "vector",
                ["content_hash"] = h.ContentHash
            })).ToList();

        return result;
    }

    public async Task<LongTermUpsertResult> UpsertAsync(
        IReadOnlyList<LongTermMemoryWrite> writes,
        CancellationToken ct = default)
    {
        if (writes.Count == 0) return new LongTermUpsertResult(0, 0);

        var inserted = 0;
        var dedupe = 0;

        foreach (var write in writes)
        {
            if (string.IsNullOrWhiteSpace(write.Content))
                continue;

            var embedding = await _embeddingProvider.CreateEmbeddingAsync(write.Content, ct);
            var hash = Sha256(write.Content);

            var metadata = write.Metadata is null
                ? "{}"
                : System.Text.Json.JsonSerializer.Serialize(write.Metadata);

            var record = new VectorRecord
            {
                ChunkId = Guid.NewGuid(),
                ConversationId = write.ConversationId,
                RunId = write.RunId,
                Persona = write.Persona,
                Ts = write.Ts,
                Content = write.Content,
                ContentHash = hash,
                Embedding = embedding,
                MetadataJson = metadata
            };

            var upsert = await _vectorStore.UpsertAsync(record, ct);
            if (upsert.Inserted) inserted++;
            else dedupe++;
        }

        return new LongTermUpsertResult(inserted, dedupe);
    }

    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
