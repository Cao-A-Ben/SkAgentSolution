using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using SKAgent.Agents.Memory;
using SKAgent.Agents.Persona;
using SKAgent.Core.Agent;

namespace SKAgent.Agents.Chat
{
    /// <summary>
    /// 【Chat 层 - 对话上下文组合器接口】
    /// 定义将 AgentContext（StepContext）转换为 ChatContext（SystemMessage + UserMessage）的契约。
    /// SKChatAgent 通过此接口获取构建好的对话上下文，再传递给 Semantic Kernel ChatCompletion。
    /// </summary>
    public interface IChatContextComposer
    {
        /// <summary>
        /// 根据步骤上下文组合对话所需的系统消息和用户消息。
        /// </summary>
        /// <param name="stepContext">当前步骤的执行上下文，包含 Input 和 State。</param>
        /// <returns>组合后的 ChatContext，包含 SystemMessage 和 UserMessage。</returns>
        ChatContext Compose(AgentContext stepContext);
    }

    /// <summary>
    /// 【Chat 层 - 对话上下文数据对象】
    /// 封装传递给 Semantic Kernel ChatHistory 的系统消息和用户消息。
    /// 由 IChatContextComposer.Compose 构建，由 SKChatAgent.ExecuteAsync 消费。
    /// </summary>
    public sealed class ChatContext
    {
        /// <summary>
        /// 系统消息（System Prompt），包含人格描述、用户画像、回复策略等。
        /// 注入到 ChatHistory.AddSystemMessage 中。
        /// </summary>
        public string SystemMessage { get; init; } = string.Empty;

        /// <summary>
        /// 用户消息，包含最近对话记忆摘要和当前任务指令。
        /// 注入到 ChatHistory.AddUserMessage 中。
        /// </summary>
        public string UserMessage { get; init; } = string.Empty;
    }

    /// <summary>
    /// 【Chat 层 - 默认对话上下文组合器实现】
    /// IChatContextComposer 的默认实现，负责将 Persona、Profile、Memory 组合为 ChatContext。
    /// 
    /// SystemMessage 构建逻辑：
    /// 1. 注入 PersonaOptions.SystemPrompt（人格基础描述）。
    /// 2. 如果有用户画像，追加 [User Profile] 段。
    /// 3. 如果是寒暄输入，追加短回复策略 [Response Policy]。
    /// 4. 如果是身份询问，追加硬规则 [Critical Rule]（优先使用 profile.name）。
    /// 
    /// UserMessage 构建逻辑：
    /// 1. 如果有最近对话记忆，拼接 [Recent Memory] 摘要。
    /// 2. 追加 [Current Task] 即当前步骤的指令。
    /// </summary>
    public sealed class DefaultChatContextComposer : IChatContextComposer
    {
        /// <summary>人格配置，提供 SystemPrompt 作为系统消息的基础。</summary>
        private readonly PersonaOptions _persona;

        /// <summary>
        /// 初始化默认对话上下文组合器。
        /// </summary>
        /// <param name="persona">人格配置选项。</param>
        public DefaultChatContextComposer(PersonaOptions persona)
        {
            _persona = persona;
        }

        /// <summary>
        /// 组合 ChatContext。从 stepContext.State 中读取 profile 和 recent_turns，
        /// 分别构建 SystemMessage 和 UserMessage。
        /// </summary>
        public ChatContext Compose(AgentContext stepContext)
        {
            // 1. 从 State 读取用户画像（由 RuntimeService 写入 ConversationState 后注入 stepContext.State）
            var profile = stepContext.State.TryGetValue("profile", out var p) ? p as Dictionary<string, string> : null;

            // 2. 从 State 读取最近对话记忆
            var recent = stepContext.State.TryGetValue("recent_turns", out var r) ? r as IReadOnlyList<TurnRecord> : null;

            // 3. 构建系统消息和用户消息
            var system = BuildSystem(_persona, profile, stepContext.Input);
            var user = BuildUser(stepContext.Input, recent);

            return new ChatContext
            {
                SystemMessage = system,
                UserMessage = user
            };
        }

        /// <summary>
        /// 构建系统消息（System Prompt）。
        /// 包含人格描述、用户画像、寒暄策略和身份询问硬规则。
        /// </summary>
        private static string BuildSystem(PersonaOptions persona, Dictionary<string, string>? profile, string input)
        {
            var sb = new StringBuilder();

            // 1. 注入人格基础系统提示词
            sb.AppendLine(persona.SystemPrompt.Trim());

            // 2. 如果有用户画像，追加 [User Profile] 段供 LLM 参考
            if (profile is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("[User Profile]");
                foreach (var kvp in profile)
                {
                    sb.AppendLine($"{kvp.Key}={kvp.Value}");
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

        /// <summary>
        /// 构建用户消息。将最近对话记忆摘要和当前任务拼接为 UserMessage。
        /// 包含 Prompt Budget 控制：最多 3 轮、用户输入截断 60 字、助手输出截断 80 字、总计不超过 900 字符。
        /// </summary>
        private static string BuildUser(string userInput, IReadOnlyList<TurnRecord>? recent)
        {
            // 如果没有历史记忆，直接返回当前输入
            if (recent == null || recent.Count == 0) return userInput;

            // Prompt Budget 参数
            const int maxTurns = 3;       // 最多引用 3 轮历史
            const int maxUserLen = 60;    // 用户输入摘要最大长度
            const int maxAsstLen = 80;    // 助手输出摘要最大长度
            const int maxTotalChars = 900; // 总字符数上限

            var sb = new StringBuilder();
            sb.AppendLine("[Recent  Memory]");

            // 取最近 3 轮并保持时间正序
            var selected = recent.Reverse().Take(maxTurns).Reverse().ToList();

            foreach (var t in selected)
            {
                // 对每轮的用户输入和助手输出进行单行截断
                var u = OneLine(t.UserInput, maxUserLen);
                var a = OneLine(t.AssistantOutput, maxAsstLen);
                sb.AppendLine($"- U: {u}");
                sb.AppendLine($"  A: {a}");

                // 超过总字符上限时提前终止
                if (sb.Length > maxTotalChars) break;
            }
            sb.AppendLine();
            sb.AppendLine("[Current Task]");
            sb.AppendLine(userInput);
            return sb.ToString();
        }

        /// <summary>
        /// 将多行文本压缩为单行，并截断到指定最大长度。用于 Prompt Budget 控制。
        /// </summary>
        private static string OneLine(string s, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            var t = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return t.Length < maxLen ? t : t[..maxLen] + "...";
        }

        /// <summary>
        /// 判断用户输入是否为寒暄/闲聊类。用于触发“短回复策略”。
        /// 可按需扩展关键词列表。
        /// </summary>
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

        /// <summary>
        /// 判断用户输入是否为身份询问类（如“我是谁/我叫什么”）。用于触发“优先使用 profile.name”硬规则。
        /// 可按需扩展关键词列表。
        /// </summary>
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
