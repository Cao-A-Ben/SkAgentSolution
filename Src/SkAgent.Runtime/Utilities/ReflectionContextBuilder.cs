using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Reflection;
using SkAgent.Runtime.Runtime;

namespace SKAgent.Runtime.Utilities
{
    public static class ReflectionContextBuilder
    {

        // 只放白名单 key，避免把整个 state（可能包含大对象/密钥/working_memory本体）塞进去
        private static readonly string[] StateWhitelist =
        {
        "profile",
        "persona_id",
        "recent_turns",
        "next_agent_override"
        // 你按需增减：只放“反思真的需要”的
    };
        public static ReflectionContext Build(AgentRunContext run)
        {
            var wm = WorkingMemoryAccessor.GetOrCreate(run);

            // PersonaId：优先从 state 取（你可以后面 Week6 改成 PersonaManager 的当前 persona）
            var personaId = TryGetString(run.ConversationState, "persona_id");

            // ConversationStatePreview：白名单浅拷贝
            var preview = BuildConversationStatePreview(run.ConversationState);

            return new ReflectionContext(
                RunId: run.Root.RequestId,
                ConversationId: run.ConversationId,
                UserInput: run.UserInput,
                PersonaId: personaId,
                LastStep: wm.LastStep,
                LastTool: wm.LastTool,
                RetryCounts: run.StepRetryCounts,          // 你的类型是 IReadOnlyDictionary<int,int>?，这里可直接赋
                ConversationStatePreview: preview
            );
        }


        #region Helpers

        /// <summary>
        /// 按白名单从 ConversationState 中拷贝少量字段作为 Preview。
        /// 返回 null 表示没有任何可用字段。
        /// </summary>
        private static IReadOnlyDictionary<string, object>? BuildConversationStatePreview(
            IDictionary<string, object> conversationState)
        {
            if (conversationState is null || conversationState.Count == 0)
                return null;

            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in StateWhitelist)
            {
                if (conversationState.TryGetValue(key, out var val) && val is not null)
                {
                    // ⚠️ 注意：这里只做浅拷贝（引用），避免深拷贝带来性能/一致性问题
                    dict[key] = val;
                }
            }

            return dict.Count > 0 ? dict : null;
        }

        /// <summary>
        /// 从 state 里取字符串，取不到返回 null。
        /// </summary>
        private static string? TryGetString(IDictionary<string, object> state, string key)
        {
            if (state is null) return null;
            if (!state.TryGetValue(key, out var v) || v is null) return null;

            // 常见类型直接处理
            if (v is string s) return string.IsNullOrWhiteSpace(s) ? null : s;

            // 兜底：ToString
            var t = v.ToString();
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }

        #endregion
    }
}
