using System.Security.Cryptography;
using System.Text;
using SKAgent.Core.Embedding;

namespace SKAgent.Infrastructure.Memory.Embedding;

/// <summary>
/// 默认 embedding provider（可替换为真实模型）。
/// 当前实现使用稳定哈希向量，保障离线可运行。
/// </summary>
public sealed class EmbeddingProvider : IEmbeddingProvider
{
    private readonly int _dimension;

    public EmbeddingProvider(int dimension = 128)
    {
        _dimension = Math.Max(16, dimension);
    }

    public string ModelId => $"hash-embedding-v1-{_dimension}";

    public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        text ??= string.Empty;

        var vector = new float[_dimension];
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        for (var i = 0; i < _dimension; i++)
        {
            var b = bytes[i % bytes.Length];
            vector[i] = (b - 128) / 128f;
        }

        return Task.FromResult(vector);
    }
}
