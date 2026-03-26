namespace SKAgent.Core.Memory.Vector;

public sealed class VectorRecord
{
    public Guid ChunkId { get; set; }

    public string ConversationId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string Persona { get; set; } = string.Empty;

    public DateTimeOffset Ts { get; set; } = DateTimeOffset.UtcNow;

    public string Content { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public float[] Embedding { get; set; } = [];

    public string MetadataJson { get; set; } = "{}";
}

public sealed record VectorUpsertResult(
    bool Inserted
);
