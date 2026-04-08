using SKAgent.Core.Memory;

namespace SKAgent.Core.Retrieval;

public enum RetrievalRoute
{
    RecentHistory = 1,
    ShortTerm = 2,
    Working = 3,
    Facts = 4,
    Profile = 5,
    Vector = 6,
    Tool = 7,
    Web = 8
}

public sealed record RetrievalPlan(
    IReadOnlyList<RetrievalRoute> Routes,
    IReadOnlyDictionary<RetrievalRoute, int> Budgets,
    IReadOnlyDictionary<RetrievalRoute, int> TopK,
    bool RewriteQuery,
    bool NeedClarification,
    string? SafetyPolicy,
    string Rationale
)
{
    public int GetBudget(RetrievalRoute route, int fallback = 0)
        => Budgets.TryGetValue(route, out var v) ? v : fallback;

    public int GetTopK(RetrievalRoute route, int fallback = 0)
        => TopK.TryGetValue(route, out var v) ? v : fallback;
}

public sealed record IntentRoutingResult(
    RetrievalIntent Intents,
    double Confidence,
    IReadOnlyList<string> Signals,
    RetrievalPlan Plan
);

public sealed record RetrievalFusionInput(
    IReadOnlyDictionary<RetrievalRoute, IReadOnlyList<MemoryItem>> ItemsByRoute,
    IReadOnlyDictionary<RetrievalRoute, int> Budgets
);

public sealed record RetrievalFusionResult(
    IReadOnlyList<MemoryItem> RecentHistory,
    IReadOnlyList<MemoryItem> ShortTerm,
    IReadOnlyList<MemoryItem> Working,
    IReadOnlyList<MemoryItem> LongTerm,
    IReadOnlyDictionary<RetrievalRoute, int> ByRouteCounts,
    int TotalItems,
    int BudgetUsed,
    int DedupeCount,
    int ConflictsResolved
);
