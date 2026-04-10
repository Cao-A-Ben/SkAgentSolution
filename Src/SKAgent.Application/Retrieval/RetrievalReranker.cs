using System.Text;
using System.Text.RegularExpressions;
using SKAgent.Core.Memory;
using SKAgent.Core.Modeling;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Retrieval;

/// <summary>
/// 使用现有文本生成服务对向量候选做轻量重排。
/// 当前只负责重排行为，不修改候选内容本身。
/// </summary>
public sealed class RetrievalReranker
{
    private readonly ITextGenerationService _textGeneration;
    private readonly IModelRouter _modelRouter;

    public RetrievalReranker(
        ITextGenerationService textGeneration,
        IModelRouter modelRouter)
    {
        _textGeneration = textGeneration;
        _modelRouter = modelRouter;
    }

    public async Task<IReadOnlyList<MemoryItem>> RerankAsync(
        IRunContext run,
        string query,
        IReadOnlyList<MemoryItem> candidates,
        int take,
        CancellationToken ct)
    {
        if (candidates.Count <= 1)
            return candidates.Take(Math.Max(1, take)).ToList();

        var candidateList = candidates
            .Take(Math.Max(2, Math.Min(8, candidates.Count)))
            .Select((item, index) => new RankedCandidate($"C{index + 1}", item))
            .ToList();

        var selected = _modelRouter.Select(ModelPurpose.Rerank);
        await run.EmitAsync("model_selected", new
        {
            purpose = "rerank",
            provider = selected.Provider,
            model = selected.Model,
            reason = selected.Reason
        }, ct);

        var systemPrompt = """
            You rank retrieval candidates for relevance.
            Return only candidate ids in descending relevance order.
            Rules:
            - Use only ids from the provided list.
            - Prefer candidates that best answer the current user query.
            - Prefer recent user facts over generic assistant summaries when relevance is similar.
            - Output format: C2,C1,C3
            - Do not explain.
            """;

        var userPrompt = BuildUserPrompt(query, candidateList);
        var raw = await _textGeneration.GenerateAsync(
            new TextGenerationRequest(
                systemPrompt,
                userPrompt,
                ModelPurpose.Rerank,
                Temperature: 0.1,
                TopP: 0.2),
            ct);

        var ranked = ParseRanking(raw, candidateList);
        if (ranked.Count == 0)
        {
            await run.EmitAsync("rerank_skipped", new
            {
                reason = "empty_or_unparseable_response",
                candidateCount = candidateList.Count
            }, ct);

            return candidates.Take(Math.Max(1, take)).ToList();
        }

        var result = ranked.Take(Math.Max(1, take)).ToList();
        await run.EmitAsync("rerank_applied", new
        {
            candidateCount = candidateList.Count,
            kept = result.Count,
            preview = string.Join(", ", candidateList
                .Where(c => result.Any(r => r.Id == c.Item.Id))
                .OrderBy(c => result.FindIndex(r => r.Id == c.Item.Id))
                .Select(c => c.Id))
        }, ct);

        return result;
    }

    private static string BuildUserPrompt(string query, IReadOnlyList<RankedCandidate> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Current Query]");
        sb.AppendLine(query);
        sb.AppendLine();
        sb.AppendLine("[Candidates]");

        foreach (var candidate in candidates)
        {
            sb.AppendLine($"{candidate.Id}: {TrimSingleLine(candidate.Item.Content, 240)}");
        }

        return sb.ToString();
    }

    private static List<MemoryItem> ParseRanking(string raw, IReadOnlyList<RankedCandidate> candidates)
    {
        var map = candidates.ToDictionary(x => x.Id, x => x.Item, StringComparer.OrdinalIgnoreCase);
        var result = new List<MemoryItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Regex.Matches(raw ?? string.Empty, @"C\d+", RegexOptions.IgnoreCase))
        {
            var id = match.Value.Trim();
            if (!seen.Add(id))
                continue;

            if (map.TryGetValue(id, out var item))
                result.Add(item);
        }

        if (result.Count == 0)
            return [];

        foreach (var candidate in candidates)
        {
            if (seen.Add(candidate.Id))
                result.Add(candidate.Item);
        }

        return result;
    }

    private static string TrimSingleLine(string text, int maxChars)
    {
        var singleLine = (text ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return singleLine.Length <= maxChars
            ? singleLine
            : singleLine[..maxChars] + "...";
    }

    private sealed record RankedCandidate(string Id, MemoryItem Item);
}
