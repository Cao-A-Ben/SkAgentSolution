using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Persona;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Chat
{
    public interface IChatContextComposer
    {
        ChatContext Compose(AgentContext stepContext);
    }

    public sealed class ChatContext
    {
        public string SystemMessage { get; init; } = string.Empty;

        public string UserMessage { get; init; } = string.Empty;
    }

    public sealed class DefaultChatContextComposer : IChatContextComposer
    {

        private readonly PersonaOptions _persona;


        public DefaultChatContextComposer(PersonaOptions persona)
        {
            _persona = persona;
        }

        public ChatContext Compose(AgentContext stepContext)
        {

            // 从 state 读取 profile/memory（由 RuntimeService/Executor 写入 ConversationState 后注入 stepContext.State）
            var profile = stepContext.State.TryGetValue("profile", out var p) ? p as Dictionary<string, string> : null;
            var recent = stepContext.State.TryGetValue("recent_turns", out var r) ? r as IReadOnlyList<TurnRecord> : null;

            var system = BuildSystem(_persona, profile);
            var user = BuildUser(stepContext.Input, recent);

            return new ChatContext
            {
                SystemMessage = system,
                UserMessage = user
            };
        }

        private static string BuildSystem(PersonaOptions persona, Dictionary<string, string>? profile)
        {
            var sb = new StringBuilder();
            sb.AppendLine(persona.SystemPrompt.Trim());

            if (profile is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("[User Profile]");
                foreach (var kvp in profile)
                {
                    sb.AppendLine($"{kvp.Key}= {kvp.Value}");
                }
            }
            return sb.ToString();
        }

        private static string BuildUser(string userInput, IReadOnlyList<TurnRecord>? recent)
        {
            // 这里的 userInput 是 stepContext.Input（即 PlanStep.Instruction 或用户直接输入）
            // recent 是最近 N 轮记忆（可裁剪）
            if (recent == null || recent.Count == 0) return userInput;
            var sb = new StringBuilder();
            sb.AppendLine("[Recent  Memory]");
            foreach (var t in recent)
            {
                var u = t.UserInput.Replace("\n", " ").Trim();
                var a = t.AssistantOutput.Replace("\n", " ").Trim();
                if (a.Length > 180) a = a[..180] + "...";
                sb.AppendLine($"- User: {u}");
                sb.AppendLine($"  Assistant: {a}");
            }
            sb.AppendLine();
            sb.AppendLine("[Current Task]");
            sb.AppendLine(userInput);
            return sb.ToString();
        }
    }
}
