using SKAgent.Core.Memory;

namespace SKAgent.Core.Retrieval;

public enum RetrievalRoute
{
    ShortTerm = 1,
    Working = 2,
    Facts = 3,
    Profile = 4,
    Vector = 5,
    Tool = 6,
    Web = 7
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
    IReadOnlyList<MemoryItem> ShortTerm,
    IReadOnlyList<MemoryItem> Working,
    IReadOnlyList<MemoryItem> LongTerm,
    IReadOnlyDictionary<RetrievalRoute, int> ByRouteCounts,
    int TotalItems,
    int BudgetUsed,
    int DedupeCount,
    int ConflictsResolved
);
