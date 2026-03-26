using System.Security.Cryptography;
using System.Text;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.Facts;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Memory.Working;
using SKAgent.Core.Personas;
using SKAgent.Core.Retrieval;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Memory;

/// <summary>
/// Week7 记忆编排器：short/working/facts/profile/vector 多路融合。
/// </summary>
public sealed class MemoryOrchestrator
{
    private readonly IShortTermMemory _short;
    private readonly IWorkingMemoryStore _working;
    private readonly ILongTermMemory _long;
    private readonly IFactStore _facts;
    private readonly IQueryRewriter _queryRewriter;
    private readonly IRetrievalFusion _retrievalFusion;
    private readonly MemoryBudgeter _budgeter;

    public MemoryOrchestrator(
        IShortTermMemory shortTerm,
        IWorkingMemoryStore working,
        ILongTermMemory longTerm,
        IFactStore facts,
        IQueryRewriter queryRewriter,
        IRetrievalFusion retrievalFusion,
        MemoryBudgeter budgeter)
    {
        _short = shortTerm;
        _working = working;
        _long = longTerm;
        _facts = facts;
        _queryRewriter = queryRewriter;
        _retrievalFusion = retrievalFusion;
        _budgeter = budgeter;
    }

    public async Task<MemoryBundle> BuildAsync(
        IRunContext run,
        PersonaOptions persona,
        string userInput,
        CancellationToken ct)
    {
        var routing = ResolvePlan(run, persona);

        var conversationId = run.ConversationId;
        var shortRaw = await LoadShortTermAsync(run, conversationId, ct);
        var workingRaw = await _working.ListAsync(conversationId, ct);
        var factsRaw = await LoadFactsAsync(conversationId, ct);
        var profileRaw = LoadProfile(run);
        var vectorRaw = await LoadVectorAsync(run, routing, userInput, ct);

        var byRoute = new Dictionary<RetrievalRoute, IReadOnlyList<MemoryItem>>
        {
            [RetrievalRoute.ShortTerm] = shortRaw,
            [RetrievalRoute.Working] = workingRaw,
            [RetrievalRoute.Facts] = factsRaw,
            [RetrievalRoute.Profile] = profileRaw,
            [RetrievalRoute.Vector] = vectorRaw
        };

        var fused = _retrievalFusion.Fuse(new RetrievalFusionInput(byRoute, routing.Plan.Budgets));
        var shortItems = _budgeter.ClipByChars(fused.ShortTerm, routing.Plan.GetBudget(RetrievalRoute.ShortTerm, 4000), out var shortReason);
        var workingItems = _budgeter.ClipByChars(fused.Working, routing.Plan.GetBudget(RetrievalRoute.Working, 3000), out var workingReason);
        var longItems = _budgeter.ClipByChars(fused.LongTerm, routing.Plan.GetBudget(RetrievalRoute.Vector, 4000) + routing.Plan.GetBudget(RetrievalRoute.Facts, 2000), out var longReason);

        await run.EmitAsync("memory_retrieved_long_term", new
        {
            queryHash = Sha256(userInput),
            candidates = vectorRaw.Count,
            kept = longItems.Count,
            budgetChars = routing.Plan.GetBudget(RetrievalRoute.Vector, 4000),
            dedupeCount = fused.DedupeCount,
            truncateReason = longReason
        }, ct);

        await run.EmitAsync("memory_fused", new
        {
            byRouteCounts = fused.ByRouteCounts.ToDictionary(k => k.Key.ToString().ToLowerInvariant(), v => v.Value),
            totalItems = fused.TotalItems,
            budgetUsed = fused.BudgetUsed,
            conflictsResolved = fused.ConflictsResolved
        }, ct);

        await run.EmitAsync("memory_layer_included", new
        {
            layer = "short-term",
            countBefore = fused.ShortTerm.Count,
            countAfter = shortItems.Count,
            budgetChars = routing.Plan.GetBudget(RetrievalRoute.ShortTerm, 4000),
            truncateReason = shortReason
        }, ct);

        await run.EmitAsync("memory_layer_included", new
        {
            layer = "working",
            countBefore = fused.Working.Count,
            countAfter = workingItems.Count,
            budgetChars = routing.Plan.GetBudget(RetrievalRoute.Working, 3000),
            truncateReason = workingReason
        }, ct);

        await run.EmitAsync("memory_layer_included", new
        {
            layer = "long-term",
            countBefore = fused.LongTerm.Count,
            countAfter = longItems.Count,
            budgetChars = routing.Plan.GetBudget(RetrievalRoute.Vector, 4000) + routing.Plan.GetBudget(RetrievalRoute.Facts, 2000),
            truncateReason = longReason
        }, ct);

        if (routing.Intents.HasFlag(RetrievalIntent.HealthSensitive))
        {
            if (!longItems.Any(i => i.Content.Contains("免责声明", StringComparison.OrdinalIgnoreCase)))
            {
                var disclaimers = new List<MemoryItem>(longItems)
                {
                    new(
                        Id: $"safety:{run.RunId}",
                        Layer: MemoryLayer.LongTerm,
                        Content: "免责声明：以下内容仅供健康科普参考，不能替代专业医疗诊断与治疗建议。",
                        At: DateTimeOffset.UtcNow,
                        Score: 1.0,
                        Metadata: new Dictionary<string, string>
                        {
                            ["route"] = "facts",
                            ["policy"] = routing.Plan.SafetyPolicy ?? "health_sensitive_v1"
                        })
                };
                longItems = disclaimers;
            }

            await run.EmitAsync("safety_policy_applied", new
            {
                policyId = routing.Plan.SafetyPolicy ?? "health_sensitive_v1",
                reason = "health_sensitive_intent"
            }, ct);
        }

        return new MemoryBundle(shortItems, workingItems, longItems);
    }

    private static IntentRoutingResult ResolvePlan(IRunContext run, PersonaOptions persona)
    {
        if (run.ConversationState.TryGetValue("retrieval_plan", out var p)
            && p is RetrievalPlan plan
            && run.ConversationState.TryGetValue("retrieval_intents", out var i)
            && i is RetrievalIntent intents)
        {
            return new IntentRoutingResult(intents, 1.0, ["state_cache"], plan);
        }

        var routes = new[]
        {
            RetrievalRoute.ShortTerm,
            RetrievalRoute.Working,
            RetrievalRoute.Profile
        };

        var budgets = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.ShortTerm] = persona.Policy.Memory?.ShortTermBudgetChars ?? 4000,
            [RetrievalRoute.Working] = persona.Policy.Memory?.WorkingBudgetChars ?? 3000,
            [RetrievalRoute.Vector] = persona.Policy.Memory?.LongTermBudgetChars ?? 4000
        };

        var topK = new Dictionary<RetrievalRoute, int> { [RetrievalRoute.Vector] = 8 };
        var fallback = new RetrievalPlan(routes, budgets, topK, RewriteQuery: false, NeedClarification: false, SafetyPolicy: null, Rationale: "fallback_default_plan");
        return new IntentRoutingResult(RetrievalIntent.Chitchat, 0.5, ["fallback"], fallback);
    }

    private async Task<IReadOnlyList<MemoryItem>> LoadShortTermAsync(IRunContext run, string conversationId, CancellationToken ct)
    {
        var turns = await _short.GetRecentAsync(conversationId, take: 20, ct);
        var shortRaw = turns.Select(t => TurnToShortTermItem(conversationId, t)).ToList();

        if (shortRaw.Count == 0
            && run.ConversationState.TryGetValue("recent_turns", out var rtObj)
            && rtObj is IReadOnlyList<TurnRecord> recentTurns
            && recentTurns.Count > 0)
        {
            shortRaw = recentTurns
                .Reverse()
                .Select(t => TurnToShortTermItem(conversationId, t))
                .ToList();
        }

        return shortRaw;
    }

    private async Task<IReadOnlyList<MemoryItem>> LoadFactsAsync(string conversationId, CancellationToken ct)
    {
        var facts = await _facts.ListAsync(conversationId, ct);
        return facts.Select(f => new MemoryItem(
            Id: $"fact:{conversationId}:{f.Key}",
            Layer: MemoryLayer.LongTerm,
            Content: $"{f.Key}={f.Value}",
            At: f.Ts,
            Score: f.Confidence,
            Metadata: new Dictionary<string, string>
            {
                ["route"] = "facts",
                ["source"] = f.Source
            })).ToList();
    }

    private static IReadOnlyList<MemoryItem> LoadProfile(IRunContext run)
    {
        if (run.ConversationState.TryGetValue("profile", out var p)
            && p is IReadOnlyDictionary<string, string> profile
            && profile.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            return profile.Select(kv => new MemoryItem(
                Id: $"profile:{kv.Key}",
                Layer: MemoryLayer.LongTerm,
                Content: $"{kv.Key}={kv.Value}",
                At: now,
                Score: 0.75,
                Metadata: new Dictionary<string, string> { ["route"] = "profile" }))
                .ToList();
        }

        return [];
    }

    private async Task<IReadOnlyList<MemoryItem>> LoadVectorAsync(
        IRunContext run,
        IntentRoutingResult routing,
        string userInput,
        CancellationToken ct)
    {
        if (!routing.Plan.Routes.Contains(RetrievalRoute.Vector))
            return [];

        var queries = routing.Plan.RewriteQuery
            ? await _queryRewriter.RewriteAsync(userInput, routing.Intents, ct)
            : [userInput];

        var merged = new List<MemoryItem>();
        foreach (var queryText in queries.Take(3))
        {
            var startedAt = DateTimeOffset.UtcNow;
            var topK = routing.Plan.GetTopK(RetrievalRoute.Vector, 8);
            var items = await _long.QueryAsync(
                new MemoryQuery(run.ConversationId, queryText, TopK: topK, BudgetChars: routing.Plan.GetBudget(RetrievalRoute.Vector, 4000)),
                ct);

            merged.AddRange(items.Select(i => i with
            {
                Metadata = MergeMetadata(i.Metadata, "route", "vector")
            }));

            var elapsed = (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            var scores = items.Where(x => x.Score.HasValue).Select(x => x.Score!.Value).ToArray();
            var minScore = scores.Length == 0 ? 0 : scores.Min();
            var maxScore = scores.Length == 0 ? 0 : scores.Max();

            await run.EmitAsync("vector_query_executed", new
            {
                queryHash = Sha256(queryText),
                filters = new { conversationId = run.ConversationId },
                topK,
                latencyMs = elapsed,
                scoreRange = new { min = minScore, max = maxScore }
            }, ct);
        }

        return merged;
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(
        IReadOnlyDictionary<string, string>? source,
        string key,
        string value)
    {
        var dict = source is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
        dict[key] = value;
        return dict;
    }

    private static MemoryItem TurnToShortTermItem(string conversationId, TurnRecord t)
    {
        var content =
            $"[At] {t.At:O}\n" +
            $"[Goal] {t.Goal}\n" +
            $"[User] {t.UserInput}\n" +
            $"[Assistant] {t.AssistantOutput}";

        return new MemoryItem(
            Id: $"st:{conversationId}:{t.At.ToUnixTimeMilliseconds()}",
            Layer: MemoryLayer.ShortTerm,
            Content: content,
            At: t.At,
            Metadata: new Dictionary<string, string> { ["route"] = "short-term" });
    }

    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
