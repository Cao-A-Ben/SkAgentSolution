using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        var result = hits.Select(h =>
        {
            var metadata = ParseMetadata(h.MetadataJson);
            metadata["route"] = "vector";
            metadata["content_hash"] = h.ContentHash;
            if (!metadata.ContainsKey("role"))
                metadata["role"] = InferRole(metadata, h.Content);

            return new MemoryItem(
                Id: $"lt:{h.ChunkId:N}",
                Layer: MemoryLayer.LongTerm,
                Content: NormalizeContent(h.Content, metadata),
                At: h.Ts,
                Score: 1d / (1d + Math.Max(0.0001f, h.Distance)),
                Metadata: metadata);
        }).ToList();

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
                : JsonSerializer.Serialize(write.Metadata);

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

    private static Dictionary<string, string> ParseMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

    private static string InferRole(IReadOnlyDictionary<string, string> metadata, string content)
    {
        if (metadata.TryGetValue("source", out var source))
        {
            if (string.Equals(source, "user_input", StringComparison.OrdinalIgnoreCase)) return "user";
            if (string.Equals(source, "assistant_output", StringComparison.OrdinalIgnoreCase)) return "assistant";
        }

        if ((content ?? string.Empty).StartsWith("[user]", StringComparison.OrdinalIgnoreCase))
            return "user";
        if ((content ?? string.Empty).StartsWith("[assistant]", StringComparison.OrdinalIgnoreCase))
            return "assistant";

        return string.Empty;
    }

    private static string NormalizeContent(string content, IReadOnlyDictionary<string, string> metadata)
    {
        var raw = StripPrefix(content, "[user]");
        raw = StripPrefix(raw, "[assistant]");
        raw = SingleLine(raw);

        if (metadata.TryGetValue("role", out var role) && string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
            return $"相关历史用户原话：{Trim(raw, 180)}";

        if (metadata.TryGetValue("role", out role) && string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
            return $"相关历史助手回复摘要：{SummarizeAssistant(raw)}";

        return Trim(raw, 180);
    }

    private static string SummarizeAssistant(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.Contains("Unicode", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\\u", StringComparison.OrdinalIgnoreCase)
            || text.Contains("编码", StringComparison.OrdinalIgnoreCase)
            || text.Contains("大写", StringComparison.OrdinalIgnoreCase))
        {
            return "系统刚才对输入做了转换处理，并解释了结果。";
        }

        return Trim(text, 120);
    }

    private static string StripPrefix(string text, string prefix)
    {
        if ((text ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return text[prefix.Length..].Trim();
        return text ?? string.Empty;
    }

    private static string SingleLine(string text)
        => (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

    private static string Trim(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text.Length <= maxChars ? text : text[..maxChars] + "...";
    }

    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
