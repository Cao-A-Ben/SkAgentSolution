using System.Security.Cryptography;
using System.Text;
using SKAgent.Core.Embedding;
using SKAgent.Core.Modeling;

namespace SKAgent.Infrastructure.Memory.Embedding;

/// <summary>
/// 默认 embedding provider（可替换为真实模型）。
/// 当前实现使用稳定哈希向量，保障离线可运行。
/// 它会读取 ModelRouter 的 Embedding 路由，但目前仅支持 local/hash 配置。
/// </summary>
public sealed class EmbeddingProvider : IEmbeddingProvider
{
    private readonly IModelRouter? _modelRouter;
    private readonly int _defaultDimension;

    public EmbeddingProvider(IModelRouter? modelRouter = null, int dimension = 128)
    {
        _modelRouter = modelRouter;
        _defaultDimension = Math.Max(16, dimension);
    }

    public string ModelId => ResolveEffectiveModelId().modelId;

    public Task<float[]> CreateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        text ??= string.Empty;

        var (_, dimension) = ResolveEffectiveModelId();
        var vector = new float[dimension];
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        for (var i = 0; i < dimension; i++)
        {
            var b = bytes[i % bytes.Length];
            vector[i] = (b - 128) / 128f;
        }

        return Task.FromResult(vector);
    }

    private (string modelId, int dimension) ResolveEffectiveModelId()
    {
        var selection = _modelRouter?.Select(ModelPurpose.Embedding);
        if (selection is not null
            && string.Equals(selection.Provider, "local", StringComparison.OrdinalIgnoreCase)
            && TryParseHashDimension(selection.Model, out var configuredDimension))
        {
            return (selection.Model, configuredDimension);
        }

        return ($"hash-embedding-v1-{_defaultDimension}", _defaultDimension);
    }

    private static bool TryParseHashDimension(string? model, out int dimension)
    {
        dimension = 0;
        if (string.IsNullOrWhiteSpace(model))
            return false;

        const string prefix = "hash-embedding-v1-";
        if (!model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!int.TryParse(model[prefix.Length..], out var parsed))
            return false;

        dimension = Math.Max(16, parsed);
        return true;
    }
}
