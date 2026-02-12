using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

            var system = BuildSystem(_persona, profile, stepContext.Input);
            var user = BuildUser(stepContext.Input, recent);

            return new ChatContext
            {
                SystemMessage = system,
                UserMessage = user
            };
        }

        private static string BuildSystem(PersonaOptions persona, Dictionary<string, string>? profile, string input)
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

            // ① 寒暄短回复策略
            if (IsGreetingOrSmallTalk(input))
            {
                sb.AppendLine();
                sb.AppendLine("[Response Policy]");
                sb.AppendLine("当前为寒暄/建立关系场景：请用不超过2句话回复，并追加1个澄清问题。不要输出长列表。");
            }

            // ② “我是谁/我叫什么”硬规则（优先 profile.name）
            if (IsAskingIdentity(input))
            {
                sb.AppendLine();
                sb.AppendLine("[Critical Rule]");
                sb.AppendLine("如果用户询问“我是谁/我叫什么”，必须优先使用 User Profile 中的 name 字段作答。");
                sb.AppendLine("若 name 缺失或不确定，必须说明不知道并反问用户姓名；禁止杜撰或改写。");
            }
            return sb.ToString();
        }

        private static string BuildUser(string userInput, IReadOnlyList<TurnRecord>? recent)
        {
            // 这里的 userInput 是 stepContext.Input（即 PlanStep.Instruction 或用户直接输入）
            // recent 是最近 N 轮记忆（可裁剪）
            if (recent == null || recent.Count == 0) return userInput;

            const int maxTurns = 3;
            const int maxUserLen = 60;
            const int maxAsstLen = 80;
            const int maxTotalChars = 900;

            var sb = new StringBuilder();
            sb.AppendLine("[Recent  Memory]");

            var selected = recent.Reverse().Take(maxTurns).Reverse().ToList();

            foreach (var t in selected)
            {
                var u = OneLine(t.UserInput, maxUserLen);
                var a = OneLine(t.AssistantOutput, maxAsstLen);
                sb.AppendLine($"- U: {u}");
                sb.AppendLine($"  A: {a}");

                if (sb.Length > maxTotalChars) break;
            }
            sb.AppendLine();
            sb.AppendLine("[Current Task]");
            sb.AppendLine(userInput);
            return sb.ToString();
        }

        private static string OneLine(string s, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            var t = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return t.Length < maxLen ? t : t[..maxLen] + "...";
        }

        private static bool IsGreetingOrSmallTalk(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return true;

            var t = input.Trim();

            // 你可以按需扩展
            return t is "你好" or "您好" or "hi" or "hello"
                || t.Contains("在吗")
                || t.Contains("你是谁")
                || t.Contains("你叫什么")
                || t.Contains("我是谁")
                || t.Contains("我叫什么")
                || t.StartsWith("你好", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("嗨", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAskingIdentity(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var t = input.Trim();
            // 可以按需扩展
            return t.Contains("你是谁")
                || t.Contains("你叫什么")
                || t.Contains("我是谁")
                || t.Contains("我叫什么");
        }
    }
}
