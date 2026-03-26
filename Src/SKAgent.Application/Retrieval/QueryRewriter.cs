using SKAgent.Core.Retrieval;

namespace SKAgent.Application.Retrieval;

public sealed class QueryRewriter : IQueryRewriter
{
    public Task<IReadOnlyList<string>> RewriteAsync(
        string input,
        RetrievalIntent intents,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var queries = new List<string>();
        var normalized = (input ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
            queries.Add(normalized);

        if (intents.HasFlag(RetrievalIntent.Recall))
            queries.Add($"提取与用户长期目标/偏好相关内容：{normalized}");

        if (intents.HasFlag(RetrievalIntent.HealthSensitive))
            queries.Add($"提取禁忌与高风险提示：{normalized}");

        if (intents.HasFlag(RetrievalIntent.PreferenceUpdate))
            queries.Add($"提取稳定偏好与习惯：{normalized}");

        var result = queries
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(result);
    }
}
