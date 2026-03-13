using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Personas;
using SKAgent.Core.Planning;
using SKAgent.Core.Runtime;

namespace SkAgent.Runtime.Planning
{
    public sealed class DefaultPlanRequestFactory : IPlanRequestFactory
    {
        public IPlanner.PlanRequest Create(IRunContext run)
        {

            var userInputForPlanner =run.ConversationState.TryGetValue("planner_input", out var pi)
                && pi is string s && !string.IsNullOrWhiteSpace(s)? s: run.UserInput;
            // recent_turns：优先从 ConversationState 取（SSOT）
            var recentTurns =
                run.ConversationState.TryGetValue("recent_turns", out var rt) && rt is IReadOnlyList<TurnRecord> turns
                    ? turns
                    : Array.Empty<TurnRecord>();

            // profile
            var profile =
                run.ConversationState.TryGetValue("profile", out var p) && p is IReadOnlyDictionary<string, string> prof
                    ? prof
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // persona / hint
            var hint = "";
            if (run.ConversationState.TryGetValue("persona", out var po) && po is PersonaOptions persona)
                hint = persona.PlannerHint ?? "";

            // debug flag（可选）
            var debug =
                run.ConversationState.TryGetValue("debug_plan", out var dbg) && dbg is bool b && b;

            return new IPlanner.PlanRequest(
                RunId: run.RunId,
                ConversationId: run.ConversationId,
                UserInput: userInputForPlanner,
                RecentTurns: recentTurns,
                Profile: profile,
                PlannerHint: hint,
                DebugPlan: debug
            );
        }
    }
}
