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

        if (intents.HasFlag(RetrievalIntent.Recall) && IsProgressSummaryRequest(normalized))
        {
            queries.Add($"提取最近阶段推进的功能主题与里程碑：{normalized}");
            queries.Add($"提取与 persona、coach、Daily Suggestion、幂等、conversation 相关的进展：{normalized}");
            queries.Add($"提取与 planner、chat、daily、embedding、model routing、rerank 相关的进展：{normalized}");
        }
        else if (intents.HasFlag(RetrievalIntent.Recall))
        {
            queries.Add($"提取与用户长期目标/偏好相关内容：{normalized}");
        }

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

    private static bool IsProgressSummaryRequest(string input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return (text.Contains("总结", StringComparison.OrdinalIgnoreCase)
                || text.Contains("回顾", StringComparison.OrdinalIgnoreCase)
                || text.Contains("汇总", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("最近", StringComparison.OrdinalIgnoreCase)
                || text.Contains("这段时间", StringComparison.OrdinalIgnoreCase)
                || text.Contains("主要推进", StringComparison.OrdinalIgnoreCase)
                || text.Contains("主要做", StringComparison.OrdinalIgnoreCase)
                || text.Contains("完成了什么", StringComparison.OrdinalIgnoreCase)
                || text.Contains("进展", StringComparison.OrdinalIgnoreCase));
    }
}
