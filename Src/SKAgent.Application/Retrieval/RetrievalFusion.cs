using System.Security.Cryptography;
using System.Text;
using SKAgent.Core.Memory;
using SKAgent.Core.Retrieval;

namespace SKAgent.Application.Retrieval;

/// <summary>
/// 多路检索融合：去重、冲突优先级、预算裁剪。
/// </summary>
public sealed class RetrievalFusion : IRetrievalFusion
{
    public RetrievalFusionResult Fuse(RetrievalFusionInput input)
    {
        var byRoute = input.ItemsByRoute;
        var budgets = input.Budgets;

        var dedupeMap = new Dictionary<string, (RetrievalRoute route, MemoryItem item, int priority)>(StringComparer.Ordinal);
        var dedupeCount = 0;
        var conflictsResolved = 0;

        foreach (var kv in byRoute)
        {
            var route = kv.Key;

            foreach (var item in kv.Value)
            {
                var priority = GetPriority(route, item);
                var key = Sha1(NormalizeForDedupe(item.Content));
                if (!dedupeMap.TryGetValue(key, out var existing))
                {
                    dedupeMap[key] = (route, item, priority);
                    continue;
                }

                dedupeCount++;
                if (priority > existing.priority ||
                    (priority == existing.priority && item.At > existing.item.At))
                {
                    dedupeMap[key] = (route, item, priority);
                    conflictsResolved++;
                }
            }
        }

        var itemsByRoute = dedupeMap.Values
            .GroupBy(v => v.route)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MemoryItem>)g.Select(v => v.item)
                    .OrderByDescending(i => GetPriority(g.Key, i))
                    .ThenByDescending(i => i.Score ?? 0)
                    .ThenByDescending(i => i.At)
                    .ToList());

        var recentHistory = Clip(
            itemsByRoute.TryGetValue(RetrievalRoute.RecentHistory, out var rh) ? rh : [],
            GetBudget(budgets, RetrievalRoute.RecentHistory));

        var shortTerm = Clip(
            itemsByRoute.TryGetValue(RetrievalRoute.ShortTerm, out var st) ? st : [],
            GetBudget(budgets, RetrievalRoute.ShortTerm));

        var working = Clip(
            itemsByRoute.TryGetValue(RetrievalRoute.Working, out var wk) ? wk : [],
            GetBudget(budgets, RetrievalRoute.Working));

        var longCandidates = new List<MemoryItem>();
        if (itemsByRoute.TryGetValue(RetrievalRoute.Facts, out var facts))
            longCandidates.AddRange(facts);
        if (itemsByRoute.TryGetValue(RetrievalRoute.Profile, out var profile))
            longCandidates.AddRange(profile);
        if (itemsByRoute.TryGetValue(RetrievalRoute.Vector, out var vector))
            longCandidates.AddRange(vector);
        if (itemsByRoute.TryGetValue(RetrievalRoute.Web, out var web))
            longCandidates.AddRange(web);

        longCandidates = longCandidates
            .OrderByDescending(i => GetPriority(GetRoute(i), i))
            .ThenByDescending(i => i.Score ?? 0)
            .ThenByDescending(i => i.At)
            .ToList();

        var longBudget = GetBudget(budgets, RetrievalRoute.Facts)
            + GetBudget(budgets, RetrievalRoute.Profile)
            + GetBudget(budgets, RetrievalRoute.Vector)
            + GetBudget(budgets, RetrievalRoute.Web);

        var longTerm = Clip(longCandidates, longBudget > 0 ? longBudget : 4000);

        var byRouteCounts = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = recentHistory.Count,
            [RetrievalRoute.ShortTerm] = shortTerm.Count,
            [RetrievalRoute.Working] = working.Count,
            [RetrievalRoute.Facts] = longTerm.Count(i => GetRoute(i) == RetrievalRoute.Facts),
            [RetrievalRoute.Profile] = longTerm.Count(i => GetRoute(i) == RetrievalRoute.Profile),
            [RetrievalRoute.Vector] = longTerm.Count(i => GetRoute(i) == RetrievalRoute.Vector)
        };

        var totalItems = recentHistory.Count + shortTerm.Count + working.Count + longTerm.Count;
        var budgetUsed = recentHistory.Sum(ContentLen) + shortTerm.Sum(ContentLen) + working.Sum(ContentLen) + longTerm.Sum(ContentLen);

        return new RetrievalFusionResult(
            RecentHistory: recentHistory,
            ShortTerm: shortTerm,
            Working: working,
            LongTerm: longTerm,
            ByRouteCounts: byRouteCounts,
            TotalItems: totalItems,
            BudgetUsed: budgetUsed,
            DedupeCount: dedupeCount,
            ConflictsResolved: conflictsResolved);
    }

    private static int GetBudget(IReadOnlyDictionary<RetrievalRoute, int> budgets, RetrievalRoute route)
        => budgets.TryGetValue(route, out var value) ? value : 0;

    private static int GetPriority(RetrievalRoute route, MemoryItem item) => route switch
    {
        RetrievalRoute.RecentHistory when GetRole(item) == "user" => 340,
        RetrievalRoute.RecentHistory when GetRole(item) == "assistant" => 330,
        RetrievalRoute.Facts => 300,
        RetrievalRoute.ShortTerm => 260,
        RetrievalRoute.Working => 250,
        RetrievalRoute.Profile => 220,
        RetrievalRoute.Vector when GetRole(item) == "user" => 190,
        RetrievalRoute.Vector => 170,
        RetrievalRoute.Web => 120,
        RetrievalRoute.Tool => 110,
        _ => 100
    };

    private static RetrievalRoute GetRoute(MemoryItem item)
    {
        if (item.Metadata?.TryGetValue("route", out var route) == true)
        {
            return route switch
            {
                "recent_history" => RetrievalRoute.RecentHistory,
                "short-term" => RetrievalRoute.ShortTerm,
                "facts" => RetrievalRoute.Facts,
                "profile" => RetrievalRoute.Profile,
                "vector" => RetrievalRoute.Vector,
                "web" => RetrievalRoute.Web,
                _ => item.Layer == MemoryLayer.Working ? RetrievalRoute.Working : RetrievalRoute.ShortTerm
            };
        }

        return item.Layer == MemoryLayer.Working ? RetrievalRoute.Working : RetrievalRoute.ShortTerm;
    }

    private static string GetRole(MemoryItem item)
    {
        if (item.Metadata?.TryGetValue("role", out var role) == true && !string.IsNullOrWhiteSpace(role))
            return role;

        if (item.Metadata?.TryGetValue("source", out var source) == true)
        {
            if (string.Equals(source, "user_input", StringComparison.OrdinalIgnoreCase)) return "user";
            if (string.Equals(source, "assistant_output", StringComparison.OrdinalIgnoreCase)) return "assistant";
        }

        var text = item.Content ?? string.Empty;
        if (text.Contains("用户原话", StringComparison.OrdinalIgnoreCase) || text.StartsWith("[user]", StringComparison.OrdinalIgnoreCase))
            return "user";
        if (text.Contains("系统处理", StringComparison.OrdinalIgnoreCase) || text.Contains("助手回复", StringComparison.OrdinalIgnoreCase) || text.StartsWith("[assistant]", StringComparison.OrdinalIgnoreCase))
            return "assistant";

        return string.Empty;
    }

    private static IReadOnlyList<MemoryItem> Clip(IReadOnlyList<MemoryItem> items, int budgetChars)
    {
        if (items.Count == 0 || budgetChars <= 0) return [];

        var result = new List<MemoryItem>(items.Count);
        var acc = 0;
        foreach (var item in items)
        {
            var len = ContentLen(item);
            if (acc + len > budgetChars) break;
            result.Add(item);
            acc += len;
        }
        return result;
    }

    private static string NormalizeForDedupe(string text)
    {
        var normalized = (text ?? string.Empty).Trim();
        var prefixes = new[]
        {
            "最近用户原话：",
            "相关历史用户原话：",
            "最近系统处理：",
            "相关历史助手回复摘要：",
            "[user] ",
            "[assistant] "
        };

        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..].Trim();
                break;
            }
        }

        return normalized;
    }

    private static int ContentLen(MemoryItem item) => (item.Content ?? string.Empty).Length;

    private static string Sha1(string text)
    {
        using var sha = SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
