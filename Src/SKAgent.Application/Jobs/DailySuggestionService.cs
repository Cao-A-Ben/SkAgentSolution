using System.Security.Cryptography;
using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Application.Persona;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Modeling;
using SKAgent.Core.Observability;
using SKAgent.Core.Personas;
using SKAgent.Core.Profile;
using SKAgent.Core.Retrieval;
using SKAgent.Core.Runtime;
using SKAgent.Core.Suggestions;

namespace SKAgent.Application.Jobs;

public sealed class DailySuggestionService
{
    private const string MetaRecallJustSaid = "\u6211\u521A\u521A\u8BF4\u4E86\u4EC0\u4E48";
    private const string MetaRecallJustNow = "\u6211\u521A\u624D\u8BF4\u4E86\u4EC0\u4E48";
    private const string MetaRecallEarlier = "\u524D\u9762\u8BF4\u4E86\u4EC0\u4E48";
    private const string MetaRemember = "\u4F60\u8BB0\u5F97";
    private const string MetaSaidBefore = "\u4E4B\u524D\u8BF4\u8FC7";
    private const string GreetingHello = "\u4F60\u597D";
    private const string GreetingHelloAh = "\u4F60\u597D\u554A";
    private const string GreetingMorning = "\u65E9\u4E0A\u597D";
    private const string GreetingEvening = "\u665A\u4E0A\u597D";

    private readonly IRunPreparationService _preparationService;
    private readonly IShortTermMemory _shortTermMemory;
    private readonly IUserProfileStore _profileStore;
    private readonly PersonaManager _personaManager;
    private readonly ISuggestionStore _suggestionStore;
    private readonly IConversationScopeResolver _conversationScopeResolver;
    private readonly ITextGenerationService _textGenerationService;
    private readonly IRunEventLogFactory _runEventLogFactory;
    private readonly DailySuggestionOptions _options;

    public DailySuggestionService(
        IRunPreparationService preparationService,
        IShortTermMemory shortTermMemory,
        IUserProfileStore profileStore,
        PersonaManager personaManager,
        ISuggestionStore suggestionStore,
        IConversationScopeResolver conversationScopeResolver,
        ITextGenerationService textGenerationService,
        IRunEventLogFactory runEventLogFactory,
        DailySuggestionOptions options)
    {
        _preparationService = preparationService;
        _shortTermMemory = shortTermMemory;
        _profileStore = profileStore;
        _personaManager = personaManager;
        _suggestionStore = suggestionStore;
        _conversationScopeResolver = conversationScopeResolver;
        _textGenerationService = textGenerationService;
        _runEventLogFactory = runEventLogFactory;
        _options = options;
    }

    public Task<IReadOnlyList<SuggestionRecord>> ListRecentAsync(int take = 30, CancellationToken ct = default)
        => _suggestionStore.ListRecentAsync(take, ct);

    public async Task<DailySuggestionResult> GenerateIfMissingAsync(
        DateOnly? date = null,
        string? personaName = null,
        string? conversationId = null,
        CancellationToken ct = default)
    {
        var options = _options;
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var resolvedConversationId = await ResolveConversationIdAsync(options, conversationId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(resolvedConversationId))
            throw new InvalidOperationException("No active conversation could be resolved for daily suggestion generation.");

        var targetPersona = string.IsNullOrWhiteSpace(personaName) ? options.PersonaName : personaName.Trim();

        var existing = await _suggestionStore.GetAsync(targetDate, resolvedConversationId, ct).ConfigureAwait(false);
        if (existing is not null)
            return new DailySuggestionResult(existing, Created: false);

        var eventLog = _runEventLogFactory.CreateDailySuggestionLog(targetDate);
        var run = new BackgroundRunContext(
            resolvedConversationId,
            "Generate one daily suggestion for today.",
            ct,
            eventLog.Sink);

        await run.EmitAsync("daily_job_started", new
        {
            date = targetDate.ToString("yyyy-MM-dd"),
            personaName = targetPersona,
            conversationId = resolvedConversationId
        }, ct).ConfigureAwait(false);

        try
        {
            var recentTurns = await _shortTermMemory.GetRecentAsync(resolvedConversationId, options.RecentTurnTake, ct).ConfigureAwait(false);
            run.ConversationState["recent_turns"] = recentTurns;

            var profile = await _profileStore.GetAsync(resolvedConversationId, ct).ConfigureAwait(false);
            run.ConversationState["profile"] = profile;

            var selectedPersona = _personaManager.GetOrSelect(run.RunId, resolvedConversationId, targetPersona);
            run.ConversationState["persona"] = selectedPersona.Persona;
            run.ConversationState["personaName"] = selectedPersona.Persona.Name;

            await run.EmitAsync("persona_selected", new
            {
                conversationId = resolvedConversationId,
                personaName = selectedPersona.Persona.Name,
                source = selectedPersona.Source,
                reason = selectedPersona.Reason
            }, ct).ConfigureAwait(false);

            run.ConversationState["retrieval_intents"] = RetrievalIntent.None;
            run.ConversationState["retrieval_plan"] = BuildDailyRetrievalPlan();

            await _preparationService.PrepareAsync(run, ct).ConfigureAwait(false);

            var memoryBundle = run.ConversationState.TryGetValue("memoryBundle", out var memoryObj)
                && memoryObj is MemoryBundle bundle
                ? bundle
                : new MemoryBundle([], [], [], []);

            var candidate = BuildSuggestedNextStepCandidate(selectedPersona.Persona, recentTurns, profile, memoryBundle);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                await run.EmitAsync("daily_suggestion_candidate_built", new
                {
                    source = "heuristic",
                    preview = candidate
                }, ct).ConfigureAwait(false);
            }

            var dailyTask = BuildDailyTask(targetDate, selectedPersona.Persona, recentTurns, profile, candidate);
            var prompt = await _preparationService
                .GetPromptAsync(run, PromptTarget.Daily, dailyTask, options.CharBudget, ct)
                .ConfigureAwait(false);

            var suggestion = (await _textGenerationService.GenerateAsync(new TextGenerationRequest(
                prompt.System,
                prompt.User,
                ModelPurpose.Daily,
                Temperature: 0.3,
                TopP: 0.9), ct).ConfigureAwait(false)).Trim();

            var record = new SuggestionRecord(
                Date: targetDate,
                Suggestion: suggestion,
                RunId: run.RunId,
                ConversationId: resolvedConversationId,
                PersonaName: selectedPersona.Persona.Name,
                ProfileHash: ComputeProfileHash(profile),
                PromptHash: prompt.Hash,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                EventLogPath: eventLog.Path);

            await _suggestionStore.SaveAsync(record, ct).ConfigureAwait(false);

            await run.EmitAsync("suggestion_saved", new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                runId = run.RunId,
                hash = prompt.Hash,
                chars = suggestion.Length,
                eventLogPath = eventLog.Path
            }, ct).ConfigureAwait(false);

            await run.EmitAsync("run_completed", new
            {
                finalOutput = suggestion,
                stepCount = 0,
                retries = new Dictionary<int, int>(),
                toolCallCount = 0
            }, ct).ConfigureAwait(false);

            await run.EmitAsync("daily_job_finished", new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                runId = run.RunId,
                created = true
            }, ct).ConfigureAwait(false);

            return new DailySuggestionResult(record, Created: true);
        }
        catch (Exception ex)
        {
            await run.EmitAsync("daily_job_failed", new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                runId = run.RunId,
                error = ex.Message
            }, ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string?> ResolveConversationIdAsync(DailySuggestionOptions options, string? conversationId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
            return conversationId.Trim();

        if (!string.IsNullOrWhiteSpace(options.ConversationId))
            return options.ConversationId.Trim();

        if (!options.UseLatestConversation)
            return null;

        return await _conversationScopeResolver.ResolveAsync(ct).ConfigureAwait(false);
    }

    private static RetrievalPlan BuildDailyRetrievalPlan()
    {
        var routes = new[]
        {
            RetrievalRoute.RecentHistory,
            RetrievalRoute.ShortTerm,
            RetrievalRoute.Working,
            RetrievalRoute.Profile,
            RetrievalRoute.Facts,
            RetrievalRoute.Vector
        };

        var budgets = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = 1600,
            [RetrievalRoute.ShortTerm] = 3200,
            [RetrievalRoute.Working] = 2000,
            [RetrievalRoute.Profile] = 1200,
            [RetrievalRoute.Facts] = 1800,
            [RetrievalRoute.Vector] = 3200
        };

        var topK = new Dictionary<RetrievalRoute, int>
        {
            [RetrievalRoute.RecentHistory] = 6,
            [RetrievalRoute.ShortTerm] = 12,
            [RetrievalRoute.Working] = 10,
            [RetrievalRoute.Profile] = 6,
            [RetrievalRoute.Facts] = 8,
            [RetrievalRoute.Vector] = 6
        };

        return new RetrievalPlan(
            Routes: routes,
            Budgets: budgets,
            TopK: topK,
            RewriteQuery: true,
            NeedClarification: false,
            SafetyPolicy: null,
            Rationale: "daily_suggestion_latest_conversation");
    }

    private static string BuildDailyTask(
        DateOnly date,
        PersonaOptions persona,
        IReadOnlyList<TurnRecord> recentTurns,
        IReadOnlyDictionary<string, string> profile,
        string? candidate)
        => $"""
Generate one daily suggestion for {date:yyyy-MM-dd}.

Return Chinese only.
You are generating a suggestion for an ongoing product or learning conversation.
Prefer continuing the current context instead of giving generic encouragement.

[PERSONA]
- name: {persona.Name}

[PERSONA STYLE]
{BuildDailyPersonaStyle(persona)}

[PROFILE SIGNALS]
{BuildProfileSummary(profile)}

[RECENT USER FOCUS]
{BuildRecentUserFocusSummary(recentTurns)}

[RECENT SYSTEM ACTIONS]
{BuildRecentActionSummary(recentTurns)}

[BEST NEXT STEP CANDIDATE]
{candidate ?? "- none"}

Requirements:
1. Give exactly one concrete next step for today.
2. If recent turns are mostly greetings, recall checks, or meta questions, still extract a real direction from profile or long-term memory instead of saying "review your conversations" or "talk more with family and friends".
3. Do not invent user facts. If information is still limited, give the safest next action that best matches the current project trajectory.
4. Output Chinese in 1 to 2 sentences, ideally within 80 Chinese characters.
5. Keep the tone warm, specific, and actionable.
6. Output only the suggestion body. No title. No bullets. No reasoning.
7. If BEST NEXT STEP CANDIDATE is concrete and consistent with the context, use it directly or only make minimal wording changes.
""";

    private static string? BuildSuggestedNextStepCandidate(
        PersonaOptions persona,
        IReadOnlyList<TurnRecord> recentTurns,
        IReadOnlyDictionary<string, string> profile,
        MemoryBundle bundle)
    {
        var signals = new List<string>();

        signals.AddRange(recentTurns.Select(x => x.UserInput).Where(x => !string.IsNullOrWhiteSpace(x))!);
        signals.AddRange(recentTurns.Select(x => x.Goal).Where(x => !string.IsNullOrWhiteSpace(x))!);
        signals.AddRange(profile.Select(kv => $"{kv.Key}:{kv.Value}"));
        signals.AddRange(bundle.RecentHistory.Select(x => x.Content));
        signals.AddRange(bundle.LongTerm.Select(x => x.Content));

        var joined = string.Join("\n", signals);

        if (ContainsAny(joined, "daily suggestion", "每日建议", "suggestion", "prompt", "提示词")
            && ContainsAny(joined, "quality", "质量", "优化", "improve", "improving"))
        {
            return string.Equals(persona.Name, "coach", StringComparison.OrdinalIgnoreCase)
                ? "今天先把 Daily Suggestion 的 prompt 再收紧一点，再用一个新日期重跑验收，把结果收敛成下一步可执行动作。"
                : "今天先优化 Daily Suggestion 的 prompt，再用一个新日期重跑验收，确认建议内容更贴近当前项目。";
        }

        if (ContainsAny(joined, "week8", "Week8", "验收", "acceptance", "/api/suggestions", "runbook"))
        {
            return string.Equals(persona.Name, "coach", StringComparison.OrdinalIgnoreCase)
                ? "今天先整理 Week8 已验证通过的结果，再挑一个最关键的优化点继续推进，并补一次新的建议生成验证。"
                : "今天先整理 Week8 的验收结论与下一步优化项，再补一次新的建议生成验证。";
        }

        if (ContainsAny(joined, "memory", "记忆", "recall", "retrieval", "pgvector", "fusion"))
        {
            return "今天先把记忆检索与每日建议的连接点再梳理一遍，明确下一轮内容优化要优先利用哪些上下文。";
        }

        if (ContainsAny(joined, "docs", "文档", "architecture", "roadmap"))
        {
            return "今天先把文档和当前实现状态对齐，再继续推进下一个最小可验收改动。";
        }

        var actionSignals = recentTurns
            .OrderByDescending(x => x.At)
            .SelectMany(turn => turn.Steps
                .Where(step => !string.Equals(step.Target, "chat", StringComparison.OrdinalIgnoreCase))
                .Select(step => string.IsNullOrWhiteSpace(step.Target) ? step.Kind : $"{step.Kind}:{step.Target}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (actionSignals.Count > 0)
        {
            return string.Equals(persona.Name, "coach", StringComparison.OrdinalIgnoreCase)
                ? "今天先整理当前功能里已经验证通过的部分，再围绕最近这条链路做一轮更聚焦的质量回归，明确下一个动作。"
                : "今天先整理当前功能的验收结果，再围绕最近的功能链路做一轮内容质量优化验证。";
        }

        var meaningfulInputs = recentTurns
            .OrderByDescending(x => x.At)
            .Select(x => x.UserInput?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsMetaRecallInput(x!))
            .Where(x => !IsGreeting(x!))
            .ToList();

        if (meaningfulInputs.Count > 0)
        {
            return string.Equals(persona.Name, "coach", StringComparison.OrdinalIgnoreCase)
                ? "今天先从最近最重要的话题里挑一个继续往前推，把它收敛成今天能完成的一步。"
                : "今天先从最近最重要的话题里选一个继续推进，把结果收敛成一项可验证的下一步。";
        }

        if (bundle.LongTerm.Count > 0 || bundle.RecentHistory.Count > 0)
        {
            return string.Equals(persona.Name, "coach", StringComparison.OrdinalIgnoreCase)
                ? "今天先整理当前会话里已经验证通过的结果，再补一次新的质量回归，把下一步方向收成一个更具体的动作。"
                : "今天先整理当前会话里已经验证通过的结果，再补一次新的质量回归，确认下一步方向更具体。";
        }

        return string.Equals(persona.Name, "coach", StringComparison.OrdinalIgnoreCase)
            ? "今天先把当前进展整理成一个最小可执行动作，先完成它，再继续推进最接近完成的功能。"
            : "今天先把当前进展整理成一项可验证的下一步，再继续推进最接近完成的功能。";
    }

    private static string BuildDailyPersonaStyle(PersonaOptions persona)
    {
        if (string.Equals(persona.Name, "coach", StringComparison.OrdinalIgnoreCase))
        {
            return """
- Use a coaching tone: warm, steady, and action-oriented.
- Prefer helping the user keep momentum over giving generic encouragement.
- Bias toward one concrete next step for today.
- If the context is vague, narrow it to the smallest meaningful action instead of suggesting broad reflection.
""";
        }

        return """
- Keep the suggestion natural, concise, and broadly helpful.
- Prefer one actionable next step, but do not over-coach if the context is light.
""";
    }

    private static string BuildProfileSummary(IReadOnlyDictionary<string, string> profile)
    {
        if (profile.Count == 0)
            return "- none";

        return string.Join(Environment.NewLine, profile
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(kv => $"- {kv.Key}: {kv.Value}"));
    }

    private static string BuildRecentUserFocusSummary(IReadOnlyList<TurnRecord> recentTurns)
    {
        var meaningfulInputs = recentTurns
            .OrderByDescending(x => x.At)
            .Select(x => x.UserInput?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !IsMetaRecallInput(x!))
            .Where(x => !IsGreeting(x!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        if (meaningfulInputs.Count == 0)
            return "- recent inputs are mostly greeting or memory-check turns; infer the next step from long-term memory and profile";

        return string.Join(Environment.NewLine, meaningfulInputs.Select(x => $"- {x}"));
    }

    private static string BuildRecentActionSummary(IReadOnlyList<TurnRecord> recentTurns)
    {
        var actionSignals = recentTurns
            .OrderByDescending(x => x.At)
            .SelectMany(turn =>
            {
                var values = new List<string>();

                if (!string.IsNullOrWhiteSpace(turn.Goal))
                    values.Add(turn.Goal.Trim());

                foreach (var step in turn.Steps)
                {
                    if (string.Equals(step.Target, "chat", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var action = string.IsNullOrWhiteSpace(step.Target)
                        ? step.Kind
                        : $"{step.Kind}:{step.Target}";
                    values.Add(action);
                }

                return values;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        if (actionSignals.Count == 0)
            return "- no explicit tool or step action was found recently";

        return string.Join(Environment.NewLine, actionSignals.Select(x => $"- {x}"));
    }

    private static bool IsMetaRecallInput(string input)
    {
        var value = input.Trim();
        return value.Contains(MetaRecallJustSaid, StringComparison.OrdinalIgnoreCase)
            || value.Contains(MetaRecallJustNow, StringComparison.OrdinalIgnoreCase)
            || value.Contains(MetaRecallEarlier, StringComparison.OrdinalIgnoreCase)
            || value.Contains(MetaRemember, StringComparison.OrdinalIgnoreCase)
            || value.Contains(MetaSaidBefore, StringComparison.OrdinalIgnoreCase)
            || value.Contains("\u521A\u521A", StringComparison.OrdinalIgnoreCase) && value.Contains("\u8BF4\u4E86\u4EC0\u4E48", StringComparison.OrdinalIgnoreCase)
            || value.Contains("\u521A\u624D", StringComparison.OrdinalIgnoreCase) && value.Contains("\u8BF4\u4E86\u4EC0\u4E48", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGreeting(string input)
    {
        var value = input.Trim();
        return string.Equals(value, GreetingHello, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, GreetingHelloAh, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, GreetingMorning, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, GreetingEvening, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "hi", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "hello", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "nihao", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string content, params string[] values)
        => values.Any(value => content.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static string ComputeProfileHash(IReadOnlyDictionary<string, string> profile)
    {
        if (profile.Count == 0)
            return "empty";

        var normalized = string.Join("|", profile
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
