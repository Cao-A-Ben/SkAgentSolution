using System.Security.Cryptography;
using System.Text;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.Facts;
using SKAgent.Core.Memory.LongTerm;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Memory.Working;
using SKAgent.Core.Personas;
using SKAgent.Application.Retrieval;
using SKAgent.Core.Retrieval;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Memory;

/// <summary>
/// Week7 记忆编排器：recent-history/short/working/facts/profile/vector 多路融合。
/// </summary>
public sealed class MemoryOrchestrator
{
    private readonly IRecentConversationHistory _recentHistory;
    private readonly IShortTermMemory _short;
    private readonly IWorkingMemoryStore _working;
    private readonly ILongTermMemory _long;
    private readonly IFactStore _facts;
    private readonly IQueryRewriter _queryRewriter;
    private readonly RetrievalReranker _retrievalReranker;
    private readonly IRetrievalFusion _retrievalFusion;
    private readonly MemoryBudgeter _budgeter;

    public MemoryOrchestrator(
        IRecentConversationHistory recentHistory,
        IShortTermMemory shortTerm,
        IWorkingMemoryStore working,
        ILongTermMemory longTerm,
        IFactStore facts,
        IQueryRewriter queryRewriter,
        RetrievalReranker retrievalReranker,
        IRetrievalFusion retrievalFusion,
        MemoryBudgeter budgeter)
    {
        _recentHistory = recentHistory;
        _short = shortTerm;
        _working = working;
        _long = longTerm;
        _facts = facts;
        _queryRewriter = queryRewriter;
        _retrievalReranker = retrievalReranker;
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
        var recentHistoryRaw = await LoadRecentHistoryAsync(run, conversationId, routing, userInput, ct);
        var shortRaw = recentHistoryRaw.Count > 0 && routing.Intents.HasFlag(RetrievalIntent.Recall)
            ? []
            : await LoadShortTermAsync(run, conversationId, ct);
        var workingRaw = await _working.ListAsync(conversationId, ct);
        var factsRaw = await LoadFactsAsync(conversationId, ct);
        var profileRaw = LoadProfile(run);
        var vectorRaw = await LoadVectorAsync(run, routing, userInput, ct);

        var byRoute = new Dictionary<RetrievalRoute, IReadOnlyList<MemoryItem>>
        {
            [RetrievalRoute.RecentHistory] = recentHistoryRaw,
            [RetrievalRoute.ShortTerm] = shortRaw,
            [RetrievalRoute.Working] = workingRaw,
            [RetrievalRoute.Facts] = factsRaw,
            [RetrievalRoute.Profile] = profileRaw,
            [RetrievalRoute.Vector] = vectorRaw
        };

        var fused = _retrievalFusion.Fuse(new RetrievalFusionInput(byRoute, routing.Plan.Budgets));
        var recentItems = _budgeter.ClipByChars(fused.RecentHistory, routing.Plan.GetBudget(RetrievalRoute.RecentHistory, 1800), out var recentReason);
        var shortItems = _budgeter.ClipByChars(fused.ShortTerm, routing.Plan.GetBudget(RetrievalRoute.ShortTerm, 4000), out var shortReason);
        var workingItems = _budgeter.ClipByChars(fused.Working, routing.Plan.GetBudget(RetrievalRoute.Working, 3000), out var workingReason);
        var longItems = _budgeter.ClipByChars(
            fused.LongTerm,
            routing.Plan.GetBudget(RetrievalRoute.Vector, 4000)
                + routing.Plan.GetBudget(RetrievalRoute.Facts, 2000)
                + routing.Plan.GetBudget(RetrievalRoute.Profile, 1200),
            out var longReason);

        await run.EmitAsync("recent_history_retrieved", new
        {
            conversationId = run.ConversationId,
            candidates = recentHistoryRaw.Count,
            kept = recentItems.Count,
            budgetChars = routing.Plan.GetBudget(RetrievalRoute.RecentHistory, 1800),
            truncateReason = recentReason
        }, ct);

        if (routing.Intents.HasFlag(RetrievalIntent.Recall))
        {
            var recallSummary = BuildRecallSummary(recentItems, longItems, userInput);
            if (!string.IsNullOrWhiteSpace(recallSummary))
            {
                run.ConversationState["recall_answer_candidate"] = recallSummary;
                await run.EmitAsync("recall_summary_built", new
                {
                    source = IsProgressSummaryRequest(userInput) ? "recent_history+long_term" : "recent_history",
                    preview = TrimSingleLine(recallSummary, 120)
                }, ct);
            }
        }

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
            byRouteCounts = fused.ByRouteCounts.ToDictionary(k => ToRouteKey(k.Key), v => v.Value),
            totalItems = fused.TotalItems,
            budgetUsed = fused.BudgetUsed,
            conflictsResolved = fused.ConflictsResolved
        }, ct);

        await run.EmitAsync("memory_layer_included", new
        {
            layer = "recent-history",
            countBefore = fused.RecentHistory.Count,
            countAfter = recentItems.Count,
            budgetChars = routing.Plan.GetBudget(RetrievalRoute.RecentHistory, 1800),
            truncateReason = recentReason
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
            budgetChars = routing.Plan.GetBudget(RetrievalRoute.Vector, 4000)
                + routing.Plan.GetBudget(RetrievalRoute.Facts, 2000)
                + routing.Plan.GetBudget(RetrievalRoute.Profile, 1200),
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

        return new MemoryBundle(recentItems, shortItems, workingItems, longItems);
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
            [RetrievalRoute.RecentHistory] = 1800,
            [RetrievalRoute.ShortTerm] = persona.Policy.Memory?.ShortTermBudgetChars ?? 4000,
            [RetrievalRoute.Working] = persona.Policy.Memory?.WorkingBudgetChars ?? 3000,
            [RetrievalRoute.Vector] = persona.Policy.Memory?.LongTermBudgetChars ?? 4000
        };

        var topK = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = 6,
            [RetrievalRoute.Vector] = 8
        };

        var fallback = new RetrievalPlan(routes, budgets, topK, RewriteQuery: false, NeedClarification: false, SafetyPolicy: null, Rationale: "fallback_default_plan");
        return new IntentRoutingResult(RetrievalIntent.Chitchat, 0.5, ["fallback"], fallback);
    }

    private async Task<IReadOnlyList<MemoryItem>> LoadRecentHistoryAsync(
        IRunContext run,
        string conversationId,
        IntentRoutingResult routing,
        string userInput,
        CancellationToken ct)
    {
        if (!routing.Plan.Routes.Contains(RetrievalRoute.RecentHistory))
            return [];

        var durableTurns = await _recentHistory.GetRecentAsync(conversationId, routing.Plan.GetTopK(RetrievalRoute.RecentHistory, 6), ct);
        var fallbackTurns = run.ConversationState.TryGetValue("recent_turns", out var rtObj)
            && rtObj is IReadOnlyList<TurnRecord> recentTurns
            ? recentTurns
            : Array.Empty<TurnRecord>();

        var turns = durableTurns.Count > 0 ? durableTurns : fallbackTurns;

        var items = new List<MemoryItem>();
        foreach (var turn in turns
            .OrderByDescending(t => t.At)
            .Where(t => !ShouldSkipTurnForRecall(t, userInput, routing.Intents))
            .Take(routing.Plan.GetTopK(RetrievalRoute.RecentHistory, 4)))
        {
            if (!string.IsNullOrWhiteSpace(turn.UserInput))
            {
                items.Add(new MemoryItem(
                    Id: $"rh:user:{conversationId}:{turn.At.ToUnixTimeMilliseconds()}",
                    Layer: MemoryLayer.ShortTerm,
                    Content: $"最近用户原话：{TrimSingleLine(turn.UserInput, 180)}",
                    At: turn.At,
                    Score: 1.0,
                    Metadata: new Dictionary<string, string>
                    {
                        ["route"] = "recent_history",
                        ["role"] = "user",
                        ["source"] = "recent_turn_user"
                    }));
            }

            var assistantSummary = BuildAssistantSummary(turn);
            if (!string.IsNullOrWhiteSpace(assistantSummary))
            {
                items.Add(new MemoryItem(
                    Id: $"rh:assistant:{conversationId}:{turn.At.ToUnixTimeMilliseconds()}",
                    Layer: MemoryLayer.ShortTerm,
                    Content: $"最近系统处理：{assistantSummary}",
                    At: turn.At,
                    Score: 0.95,
                    Metadata: new Dictionary<string, string>
                    {
                        ["route"] = "recent_history",
                        ["role"] = "assistant",
                        ["source"] = "recent_turn_assistant"
                    }));
            }
        }

        return items;
    }

    private static bool ShouldSkipTurnForRecall(TurnRecord turn, string currentUserInput, RetrievalIntent intents)
    {
        if (!intents.HasFlag(RetrievalIntent.Recall))
            return false;

        var text = turn.UserInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (IsMetaRecallQuestion(text))
            return true;

        return string.Equals(text, currentUserInput?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMetaRecallQuestion(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return input.Contains("我刚刚说了什么", StringComparison.OrdinalIgnoreCase)
            || input.Contains("我刚才说了什么", StringComparison.OrdinalIgnoreCase)
            || input.Contains("前面说了什么", StringComparison.OrdinalIgnoreCase)
            || input.Contains("刚刚说了什么", StringComparison.OrdinalIgnoreCase)
            || input.Contains("刚才说了什么", StringComparison.OrdinalIgnoreCase)
            || input.Contains("之前说了什么", StringComparison.OrdinalIgnoreCase)
            || input.Contains("前面提到什么", StringComparison.OrdinalIgnoreCase);
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

        if (merged.Count <= 1)
            return merged;

        return await _retrievalReranker.RerankAsync(
            run,
            userInput,
            merged,
            routing.Plan.GetTopK(RetrievalRoute.Vector, 8),
            ct);
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

    private static string BuildAssistantSummary(TurnRecord turn)
    {
        if (turn.Steps.Count > 0)
        {
            var toolTargets = turn.Steps
                .Where(s => string.Equals(s.Kind, "tool", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Target)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();

            if (toolTargets.Length > 0)
                return $"先调用了 {string.Join("、", toolTargets)}，然后给出回复。";

            if (turn.Steps.Count == 1 && string.Equals(turn.Steps[0].Target, "chat", StringComparison.OrdinalIgnoreCase))
                return "主要是在继续对话并给出回复。";
        }

        var text = TrimSingleLine(turn.AssistantOutput, 120);
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (text.Contains("Unicode", StringComparison.OrdinalIgnoreCase)
            || text.Contains("\\u", StringComparison.OrdinalIgnoreCase)
            || text.Contains("编码", StringComparison.OrdinalIgnoreCase)
            || text.Contains("大写", StringComparison.OrdinalIgnoreCase))
        {
            return "对你的输入做了转换处理，并解释了结果。";
        }

        return text;
    }

    private static string? BuildRecallSummary(
        IReadOnlyList<MemoryItem> recentItems,
        IReadOnlyList<MemoryItem> longItems,
        string currentUserInput)
    {
        if (recentItems.Count == 0 && longItems.Count == 0)
            return null;

        if (IsProgressSummaryRequest(currentUserInput))
            return BuildProgressSummary(recentItems, longItems, currentUserInput);

        var userQuote = recentItems
            .Where(i => i.Metadata?.TryGetValue("role", out var role) == true && role == "user")
            .Select(i => StripLabel(i.Content, "最近用户原话："))
            .FirstOrDefault(i =>
                !string.IsNullOrWhiteSpace(i)
                && !IsMetaRecallQuestion(i)
                && !string.Equals(i.Trim(), currentUserInput?.Trim(), StringComparison.OrdinalIgnoreCase));

        var assistantSummary = recentItems
            .Where(i => i.Metadata?.TryGetValue("role", out var role) == true && role == "assistant")
            .Select(i => StripLabel(i.Content, "最近系统处理："))
            .FirstOrDefault(i => !string.IsNullOrWhiteSpace(i));

        if (string.IsNullOrWhiteSpace(userQuote) && string.IsNullOrWhiteSpace(assistantSummary))
            return null;

        if (ShouldAnswerActionSummary(currentUserInput))
        {
            if (!string.IsNullOrWhiteSpace(assistantSummary))
                return $"我刚才主要是{NormalizeAssistantSummary(assistantSummary)}";

            if (!string.IsNullOrWhiteSpace(userQuote))
                return $"你刚刚对我说的是“{userQuote}”。";

            return null;
        }

        if (!string.IsNullOrWhiteSpace(userQuote))
            return $"你刚刚说了“{userQuote}”。";

        if (!string.IsNullOrWhiteSpace(assistantSummary))
            return $"我刚才主要是{NormalizeAssistantSummary(assistantSummary)}";

        return null;
    }

    private static string? BuildProgressSummary(
        IReadOnlyList<MemoryItem> recentItems,
        IReadOnlyList<MemoryItem> longItems,
        string currentUserInput)
    {
        var allSnippets = recentItems
            .Concat(longItems)
            .Select(NormalizeProgressSnippet)
            .Where(s =>
                !string.IsNullOrWhiteSpace(s)
                && !IsMetaRecallQuestion(s)
                && !string.Equals(s.Trim(), currentUserInput?.Trim(), StringComparison.OrdinalIgnoreCase)
                && !IsGreetingLike(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var snippets = allSnippets
            .Where(s => !IsLowValueProgressSnippet(s))
            .OrderByDescending(GetProgressSnippetPriority)
            .ThenByDescending(s => s.Length)
            .Where(s => GetProgressSnippetPriority(s) >= 4)
            .Take(2)
            .ToList();

        var milestoneSnippets = ExtractProgressMilestones(allSnippets)
            .Where(s => !snippets.Contains(s, StringComparer.OrdinalIgnoreCase))
            .Take(3);

        snippets.AddRange(milestoneSnippets);

        if (snippets.Count == 0)
            return null;

        return $"最近你主要推进了：{string.Join("；", snippets.Take(3))}。";
    }

    private static string StripLabel(string? text, string label)
    {
        var value = text?.Trim() ?? string.Empty;
        if (value.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            return value[label.Length..].Trim();
        return value;
    }

    private static bool ShouldAnswerActionSummary(string input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("让你做了什么", StringComparison.OrdinalIgnoreCase)
            || text.Contains("你刚才做了什么", StringComparison.OrdinalIgnoreCase)
            || text.Contains("做了什么", StringComparison.OrdinalIgnoreCase)
            || text.Contains("处理了什么", StringComparison.OrdinalIgnoreCase)
            || text.Contains("干了什么", StringComparison.OrdinalIgnoreCase);
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

    private static string NormalizeAssistantSummary(string summary)
    {
        var value = (summary ?? string.Empty).Trim().TrimEnd('。', '.', '!', '！', '?', '？');
        if (string.IsNullOrWhiteSpace(value))
            return "继续和你对话。";

        if (value.StartsWith("主要是", StringComparison.OrdinalIgnoreCase))
            value = value["主要是".Length..].Trim();

        return value + "。";
    }

    private static string NormalizeProgressSnippet(MemoryItem item)
    {
        var text = item.Content ?? string.Empty;
        text = StripLabel(text, "最近用户原话：");
        text = StripLabel(text, "最近系统处理：");
        text = StripLabel(text, "相关历史用户原话：");
        text = StripLabel(text, "相关历史助手回复摘要：");
        text = TrimSingleLine(text, 96).Trim().TrimEnd('。', '.', '!', '！', '?', '？');

        if (text.StartsWith("[user]", StringComparison.OrdinalIgnoreCase))
            text = text["[user]".Length..].Trim();
        if (text.StartsWith("[assistant]", StringComparison.OrdinalIgnoreCase))
            text = text["[assistant]".Length..].Trim();

        return text;
    }

    private static int GetProgressSnippetPriority(string text)
    {
        var score = 0;
        if (ContainsAny(text, "Week8", "Week8.5", "Week9")) score += 6;
        if (ContainsAny(text, "persona", "coach", "default")) score += 5;
        if (ContainsAny(text, "daily", "suggestion", "建议")) score += 5;
        if (ContainsAny(text, "model", "路由", "rerank", "embedding")) score += 5;
        if (ContainsAny(text, "验收", "回归", "推进", "优化", "完成")) score += 4;
        if (ContainsAny(text, "幂等", "conversation", "pgvector", "回放", "JSONL")) score += 4;
        if (ContainsAny(text, "你好", "继续对话", "请问")) score -= 10;
        if (ContainsAny(text, "我想继续", "我想", "想继续")) score -= 8;
        if (ContainsAny(text, "系统刚才对输入做了转换处理", "解释了结果", "Unicode", "编码", "大写")) score -= 20;
        if (ContainsAny(text, "你刚刚说了", "你刚刚对我说的是")) score -= 20;
        return score;
    }

    private static bool IsGreetingLike(string text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.Equals("你好", StringComparison.OrdinalIgnoreCase)
            || value.Equals("你好啊", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("你好", StringComparison.OrdinalIgnoreCase)
            || value.Contains("请问", StringComparison.OrdinalIgnoreCase)
            || value.Contains("继续对话", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLowValueProgressSnippet(string text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.StartsWith("你刚刚说了", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("你刚刚对我说的是", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("我想继续", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("我想", StringComparison.OrdinalIgnoreCase)
            || value.Contains("系统刚才对输入做了转换处理", StringComparison.OrdinalIgnoreCase)
            || value.Contains("解释了结果", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Unicode", StringComparison.OrdinalIgnoreCase)
            || value.Contains("编码", StringComparison.OrdinalIgnoreCase)
            || value.Contains("大写", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractProgressMilestones(IReadOnlyList<string> snippets)
    {
        var combined = string.Join("\n", snippets);
        var milestones = new List<string>();

        void AddIfMatched(string summary, params string[] keywords)
        {
            if (keywords.Any(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase)))
                milestones.Add(summary);
        }

        AddIfMatched("persona 切换与 coach 风格能力", "persona", "coach", "default");
        AddIfMatched("Daily Suggestion 的生成、幂等与内容优化", "daily", "suggestion", "建议", "幂等", "conversation");
        AddIfMatched("planner / chat / daily / embedding 的模型路由收敛", "model", "模型", "路由", "embedding", "planner", "chat", "daily");
        AddIfMatched("rerank 接入与检索链路增强", "rerank", "检索", "vector");
        AddIfMatched("真实环境验收、回归和事件链验证", "验收", "回归", "事件链", "JSONL", "回放");
        AddIfMatched("Week8 到 Week8.5 的推进收口", "Week8", "Week8.5");

        return milestones.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

    private static string TrimSingleLine(string text, int maxChars)
    {
        var value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        if (value.Length <= maxChars) return value;
        return value[..maxChars] + "...";
    }

    private static string ToRouteKey(RetrievalRoute route) => route switch
    {
        RetrievalRoute.RecentHistory => "recent_history",
        RetrievalRoute.ShortTerm => "shortterm",
        RetrievalRoute.Working => "working",
        RetrievalRoute.Facts => "facts",
        RetrievalRoute.Profile => "profile",
        RetrievalRoute.Vector => "vector",
        RetrievalRoute.Tool => "tool",
        RetrievalRoute.Web => "web",
        _ => route.ToString().ToLowerInvariant()
    };

    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

}
