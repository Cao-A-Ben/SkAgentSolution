namespace SKAgent.Core.Memory.Vector;

public sealed class VectorHit
{
    public Guid ChunkId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    /// <summary>
    /// pgvector distance（越小越相关）。
    /// </summary>
    public float Distance { get; set; }

    public DateTimeOffset Ts { get; set; }
}
