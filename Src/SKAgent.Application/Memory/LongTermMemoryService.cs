using System.Security.Cryptography;
using System.Text;
using SKAgent.Application.Memory.Chunker;
using SKAgent.Core.Embedding;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Memory;

/// <summary>
/// Long-term memory 入库编排：extract -> chunk -> dedupe -> upsert。
/// </summary>
public sealed class LongTermMemoryService
{
    private readonly ILongTermMemory _longTermMemory;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly MemoryExtractor _extractor;
    private readonly TurnChunker _chunker;

    public LongTermMemoryService(
        ILongTermMemory longTermMemory,
        IEmbeddingProvider embeddingProvider,
        MemoryExtractor extractor,
        TurnChunker chunker)
    {
        _longTermMemory = longTermMemory;
        _embeddingProvider = embeddingProvider;
        _extractor = extractor;
        _chunker = chunker;
    }

    public async Task PersistRunAsync(IRunContext run, CancellationToken ct = default)
    {
        var status = run.ConversationState.TryGetValue("run_status", out var statusObj)
            ? statusObj?.ToString()
            : "unknown";

        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            return;

        var personaName = run.ConversationState.TryGetValue("personaName", out var p) && p is string name
            ? name
            : "default";

        var startedAt = DateTimeOffset.UtcNow;
        var finalOutput = run.ConversationState.TryGetValue("final_output", out var fo)
            ? fo as string
            : null;

        var sourceWrites = _extractor.Extract(
            conversationId: run.ConversationId,
            runId: run.RunId,
            userInput: run.UserInput,
            finalOutput: finalOutput,
            personaName: personaName);
        if (sourceWrites.Count == 0) return;

        var writes = new List<LongTermMemoryWrite>();
        var localHashSet = new HashSet<string>(StringComparer.Ordinal);
        var totalChars = 0;

        foreach (var item in sourceWrites)
        {
            foreach (var chunk in _chunker.Chunk(item.Content))
            {
                var hash = Sha256(chunk);
                if (!localHashSet.Add(hash)) continue;

                totalChars += chunk.Length;
                writes.Add(item with
                {
                    Content = chunk,
                    Metadata = MergeMetadata(item.Metadata, "content_hash", hash)
                });
            }
        }

        if (writes.Count == 0) return;

        var result = await _longTermMemory.UpsertAsync(writes, ct);
        var latency = (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

        await run.EmitAsync("vector_upserted", new
        {
            runId = run.RunId,
            conversationId = run.ConversationId,
            chunks = result.Inserted,
            chars = totalChars,
            model = _embeddingProvider.ModelId,
            latencyMs = latency,
            dedupeCount = result.DedupeCount
        }, ct);
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(
        IReadOnlyDictionary<string, string>? metadata,
        string key,
        string value)
    {
        var merged = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);

        merged[key] = value;
        return merged;
    }

    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
