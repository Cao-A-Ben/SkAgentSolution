namespace SKAgent.Core.Embedding;

/// <summary>
/// Embedding 生成器抽象。
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// 当前 embedding 模型标识（用于事件审计）。
    /// </summary>
    string ModelId { get; }

    /// <summary>
    /// 将文本编码为向量。
    /// </summary>
    Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default);
}
