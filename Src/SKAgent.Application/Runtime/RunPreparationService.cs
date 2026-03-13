using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Application.Memory;
using SKAgent.Application.Persona;
using SKAgent.Application.Prompt;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Personas;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Runtime;

public sealed class RunPreparationService : IRunPreparationService
{
    private readonly PersonaManager _personaManager;
    private readonly MemoryOrchestrator _memoryOrchestrator;
    private readonly PromptComposer _promptComposer;

    public RunPreparationService(
        PersonaManager personaManager,
        MemoryOrchestrator memoryOrchestrator,
        PromptComposer promptComposer)
    {
        _personaManager = personaManager;
        _memoryOrchestrator = memoryOrchestrator;
        _promptComposer = promptComposer;
    }

    public async Task PrepareAsync(IRunContext run, CancellationToken ct)
    {
        // 1) persona：若已存在则跳过（避免重复事件）
        PersonaOptions persona;
        if (run.ConversationState.TryGetValue("persona", out var po) && po is PersonaOptions p)
        {
            persona = p;
        }
        else
        {
            // requestedPersonaName 先按 null（后续接 selector）
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

        // 2) memoryBundle：若已存在则跳过
        if (!run.ConversationState.ContainsKey("memoryBundle"))
        {
            // 你的 MemoryOrchestrator 目前是 BuildAsync(AgentRunContext run, ...) 版本的话，
            // 请把它签名改成 BuildAsync(IRunContext run, ...)（只用到 RunId/ConversationId/EmitAsync/ConversationState）。
            var bundle = await _memoryOrchestrator.BuildAsync(run, persona, run.UserInput, ct);
            run.ConversationState["memoryBundle"] = bundle;
        }
    }

    public async Task<ComposedPrompt> GetPromptAsync(IRunContext run, PromptTarget target, string task, int charBudget, CancellationToken ct)
    {
        // 必须先 PrepareAsync
        if (!run.ConversationState.TryGetValue("persona", out var po) || po is not PersonaOptions persona)
            throw new InvalidOperationException("Missing persona. Call PrepareAsync first.");

        if (!run.ConversationState.TryGetValue("memoryBundle", out var mb) || mb is not SKAgent.Core.Memory.MemoryBundle bundle)
            throw new InvalidOperationException("Missing memoryBundle. Call PrepareAsync first.");

        // PromptComposer 同理建议改签名 Compose(IRunContext run, ...)（产品级解耦）
        //var composed = _promptComposer.Compose(run, persona, bundle, target, task, charBudget, ct);

        // —— 产品级：Planner 的 task 不是 raw user input，而是“结构化上下文 + 当前输入”
        var finalTask = target == PromptTarget.Planner
            ? BuildPlannerTask(run, task)
            : task;

        var composed = await _promptComposer.ComposeAsync(
            run, persona, bundle, target, finalTask, charBudget, ct);

        // —— 产品级：给 Planner 的输入落一个 SSOT 字段，供 PlanRequestFactory 使用
        if (target == PromptTarget.Planner)
            run.ConversationState["planner_input"] = composed.User;

        return composed;
    }

    private static string BuildPlannerTask(IRunContext run, string currentUserInput)
    {
        var sb = new StringBuilder();

        // 1) profile
        if (run.ConversationState.TryGetValue("profile", out var p)
            && p is IReadOnlyDictionary<string, string> profile
            && profile.Count > 0)
        {
            sb.AppendLine("[User Profile]");
            foreach (var kv in profile)
                sb.AppendLine($"{kv.Key}={kv.Value}");
            sb.AppendLine();
        }

        // 2) recent turns（优先从 state）
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
}
