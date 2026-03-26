namespace SKAgent.Core.Memory.Vector;

public sealed class VectorQuery
{
    public float[] Embedding { get; set; } = [];

    public int TopK { get; set; } = 5;

    public string? ConversationId { get; set; }
}
