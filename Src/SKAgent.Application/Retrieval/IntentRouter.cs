using SKAgent.Core.Retrieval;

namespace SKAgent.Application.Retrieval;

/// <summary>
/// Rule-first 意图路由器。
/// </summary>
public sealed class IntentRouter : IIntentRouter
{
    public Task<IntentRoutingResult> RouteAsync(
        string input,
        IReadOnlyDictionary<string, string>? profile,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var text = input?.Trim() ?? string.Empty;
        var signals = new List<string>();
        var intents = RetrievalIntent.None;

        if (ContainsAny(text, "你好", "hello", "hi", "在吗", "聊聊", "最近怎么样"))
        {
            intents |= RetrievalIntent.Chitchat;
            signals.Add("greeting_or_smalltalk");
        }

        if (ContainsAny(
                text,
                "记得", "之前", "上次", "回忆", "目标", "偏好", "习惯",
                "刚刚", "刚才", "前面说", "提到", "说了什么", "刚刚说了什么", "刚才说了什么",
                "remember", "history"))
        {
            intents |= RetrievalIntent.Recall;
            signals.Add("recall_keywords");
        }

        if (ContainsAny(text, "新闻", "时间", "天气", "汇率", "查一下", "搜索", "tool", "web", "calendar"))
        {
            intents |= RetrievalIntent.ToolNeeded;
            signals.Add("tool_needed_keywords");
        }

        if (ContainsAny(text, "禁忌", "怀孕", "出血", "慢病", "针灸", "中医", "症状", "诊断", "药"))
        {
            intents |= RetrievalIntent.HealthSensitive;
            signals.Add("health_sensitive_keywords");
        }

        if (ContainsAny(text, "我喜欢", "我不喜欢", "以后", "偏好", "习惯", "我通常"))
        {
            intents |= RetrievalIntent.PreferenceUpdate;
            signals.Add("preference_update_keywords");
        }

        if (intents == RetrievalIntent.None)
        {
            intents = RetrievalIntent.Chitchat;
            signals.Add("fallback_chitchat");
        }

        var routes = BuildRoutes(intents);
        var budgets = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = 1800,
            [RetrievalRoute.ShortTerm] = 4000,
            [RetrievalRoute.Working] = 3000,
            [RetrievalRoute.Profile] = 1200,
            [RetrievalRoute.Facts] = 2000,
            [RetrievalRoute.Vector] = 4000,
            [RetrievalRoute.Tool] = 1000,
            [RetrievalRoute.Web] = 1200
        };

        var topK = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = 6,
            [RetrievalRoute.ShortTerm] = 20,
            [RetrievalRoute.Working] = 20,
            [RetrievalRoute.Profile] = 6,
            [RetrievalRoute.Facts] = 10,
            [RetrievalRoute.Vector] = 8,
            [RetrievalRoute.Tool] = 3,
            [RetrievalRoute.Web] = 3
        };

        var needClarification = intents.HasFlag(RetrievalIntent.ToolNeeded) && ContainsAny(text, "这个", "它", "那件事");
        var confidence = signals.Count >= 2 ? 0.9 : 0.72;
        var safetyPolicy = intents.HasFlag(RetrievalIntent.HealthSensitive) ? "health_sensitive_v1" : null;

        var plan = new RetrievalPlan(
            Routes: routes,
            Budgets: budgets,
            TopK: topK,
            RewriteQuery: intents.HasFlag(RetrievalIntent.Recall) || intents.HasFlag(RetrievalIntent.HealthSensitive),
            NeedClarification: needClarification,
            SafetyPolicy: safetyPolicy,
            Rationale: BuildRationale(intents, signals));

        return Task.FromResult(new IntentRoutingResult(intents, confidence, signals, plan));
    }

    private static IReadOnlyList<RetrievalRoute> BuildRoutes(RetrievalIntent intents)
    {
        var routes = new List<RetrievalRoute>();

        void Add(RetrievalRoute route)
        {
            if (!routes.Contains(route))
                routes.Add(route);
        }

        if (intents.HasFlag(RetrievalIntent.Recall))
            Add(RetrievalRoute.RecentHistory);

        Add(RetrievalRoute.ShortTerm);
        Add(RetrievalRoute.Working);
        Add(RetrievalRoute.Profile);

        if (intents.HasFlag(RetrievalIntent.Recall) || intents.HasFlag(RetrievalIntent.HealthSensitive))
            Add(RetrievalRoute.Facts);

        if (intents.HasFlag(RetrievalIntent.Recall))
            Add(RetrievalRoute.Vector);

        if (intents.HasFlag(RetrievalIntent.ToolNeeded))
            Add(RetrievalRoute.Tool);

        return routes;
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string BuildRationale(RetrievalIntent intents, IReadOnlyList<string> signals)
        => $"intents={intents}; signals={string.Join(",", signals)}";
}
