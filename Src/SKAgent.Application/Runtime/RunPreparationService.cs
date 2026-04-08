using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Application.Memory;
using SKAgent.Application.Persona;
using SKAgent.Application.Prompt;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Modeling;
using SKAgent.Core.Personas;
using SKAgent.Core.Retrieval;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Runtime;

public sealed class RunPreparationService : IRunPreparationService
{
    private readonly PersonaManager _personaManager;
    private readonly IIntentRouter _intentRouter;
    private readonly MemoryOrchestrator _memoryOrchestrator;
    private readonly PromptComposer _promptComposer;
    private readonly IModelRouter _modelRouter;

    public RunPreparationService(
        PersonaManager personaManager,
        IIntentRouter intentRouter,
        MemoryOrchestrator memoryOrchestrator,
        PromptComposer promptComposer,
        IModelRouter modelRouter)
    {
        _personaManager = personaManager;
        _intentRouter = intentRouter;
        _memoryOrchestrator = memoryOrchestrator;
        _promptComposer = promptComposer;
        _modelRouter = modelRouter;
    }

    public async Task PrepareAsync(IRunContext run, CancellationToken ct)
    {
        PersonaOptions persona;
        if (run.ConversationState.TryGetValue("persona", out var po) && po is PersonaOptions p)
        {
            persona = p;
        }
        else
        {
            var sel = _personaManager.GetOrSelect(run.RunId, run.ConversationId, requestedPersonaName: null);

            persona = sel.Persona;
            run.ConversationState["persona"] = persona;
            run.ConversationState["personaName"] = persona.Name;

            await run.EmitAsync("persona_selected", new
            {
                conversationId = run.ConversationId,
                personaName = persona.Name,
                source = sel.Source,
                reason = sel.Reason
            }, ct);
        }

        if (!run.ConversationState.ContainsKey("retrieval_plan"))
        {
            var profile = run.ConversationState.TryGetValue("profile", out var profileObj)
                ? profileObj as IReadOnlyDictionary<string, string>
                : null;

            var routing = await _intentRouter.RouteAsync(run.UserInput, profile, ct);
            run.ConversationState["retrieval_intents"] = routing.Intents;
            run.ConversationState["retrieval_plan"] = routing.Plan;

            await run.EmitAsync("intent_classified", new
            {
                intents = routing.Intents.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                confidence = routing.Confidence,
                signals = routing.Signals
            }, ct);

            await run.EmitAsync("retrieval_plan_built", new
            {
                routes = routing.Plan.Routes.Select(ToRouteKey),
                budgets = routing.Plan.Budgets.ToDictionary(k => ToRouteKey(k.Key), v => v.Value),
                topK = routing.Plan.TopK.ToDictionary(k => ToRouteKey(k.Key), v => v.Value),
                rewriteUsed = routing.Plan.RewriteQuery,
                needClarification = routing.Plan.NeedClarification,
                safetyPolicy = routing.Plan.SafetyPolicy,
                rationale = routing.Plan.Rationale
            }, ct);
        }

        if (!run.ConversationState.ContainsKey("memoryBundle"))
        {
            var bundle = await _memoryOrchestrator.BuildAsync(run, persona, run.UserInput, ct);
            run.ConversationState["memoryBundle"] = bundle;
        }
    }

    public async Task<ComposedPrompt> GetPromptAsync(IRunContext run, PromptTarget target, string task, int charBudget, CancellationToken ct)
    {
        if (!run.ConversationState.TryGetValue("persona", out var po) || po is not PersonaOptions persona)
            throw new InvalidOperationException("Missing persona. Call PrepareAsync first.");

        if (!run.ConversationState.TryGetValue("memoryBundle", out var mb) || mb is not SKAgent.Core.Memory.MemoryBundle bundle)
            throw new InvalidOperationException("Missing memoryBundle. Call PrepareAsync first.");

        var finalTask = target == PromptTarget.Planner
            ? BuildPlannerTask(run, task)
            : task;

        var purpose = target switch
        {
            PromptTarget.Planner => ModelPurpose.Planner,
            PromptTarget.Chat => ModelPurpose.Chat,
            _ => ModelPurpose.Chat
        };

        var selected = _modelRouter.Select(purpose);
        await run.EmitAsync("model_selected", new
        {
            purpose = selected.Purpose.ToString().ToLowerInvariant(),
            provider = selected.Provider,
            model = selected.Model,
            reason = selected.Reason
        }, ct);

        var composed = await _promptComposer.ComposeAsync(
            run, persona, bundle, target, finalTask, charBudget, ct);

        if (target == PromptTarget.Planner)
            run.ConversationState["planner_input"] = composed.User;

        return composed;
    }

    private static string BuildPlannerTask(IRunContext run, string currentUserInput)
    {
        var sb = new StringBuilder();

        if (run.ConversationState.TryGetValue("profile", out var p)
            && p is IReadOnlyDictionary<string, string> profile
            && profile.Count > 0)
        {
            sb.AppendLine("[User Profile]");
            foreach (var kv in profile)
                sb.AppendLine($"{kv.Key}={kv.Value}");
            sb.AppendLine();
        }

        if (run.ConversationState.TryGetValue("recent_turns", out var r)
            && r is IReadOnlyList<TurnRecord> recent
            && recent.Count > 0)
        {
            sb.AppendLine("[Recent Conversation Memory]");
            foreach (var t in recent)
            {
                var u = (t.UserInput ?? "").Replace("\n", " ").Trim();
                var a = (t.AssistantOutput ?? "").Replace("\n", " ").Trim();
                if (a.Length > 180) a = a[..180] + "...";

                sb.AppendLine($"- User: {u}");
                sb.AppendLine($"  Assistant: {a}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("[Current User Input]");
        sb.AppendLine(currentUserInput);

        return sb.ToString();
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
}
