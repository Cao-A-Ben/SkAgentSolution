using System;
using System.Collections.Generic;
using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Application.Prompt;
using SKAgent.Application.Runtime;
using SKAgent.Core.Agent;
using SKAgent.Core.Chat;
using SKAgent.Core.Memory;
using SKAgent.Core.Memory.ShortTerm;
using SKAgent.Core.Personas;
using SKAgent.Core.Runtime;

//它是“prompt 组装/上下文编排”的用例层逻辑，会依赖 persona/memory policy（Week6），不应该属于具体 agent。
namespace SKAgent.Application.Chat
{
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
        //private readonly PersonaOptions _persona;

        //private readonly PromptComposer _promptComposer;
        private readonly IRunPreparationService _prep;
        /// <summary>
        /// 初始化默认对话上下文组合器。
        /// </summary>
        /// <param name="persona">人格配置选项。</param>
        public DefaultChatContextComposer(IRunPreparationService prep)
        {
            _prep = prep;
        }

        /// <summary>
        /// 组合 ChatContext。从 stepContext.State 中读取 profile 和 recent_turns，
        /// 分别构建 SystemMessage 和 UserMessage。
        /// </summary>
        public ChatContext Compose(AgentContext stepContext)
        {

            // 0) 取 run（W6-3 的桥接关键）
            if (!stepContext.State.TryGetValue("run", out var ro) || ro is not IRunContext run)
                throw new InvalidOperationException("Missing IRunContext in stepContext.State (key=run).");

            // 1) persona：从 state 取（W6-1 已写入 ConversationState 并复制到 stepContext）
            if (!stepContext.State.TryGetValue("persona", out var po) || po is not PersonaOptions persona)
                throw new InvalidOperationException("Missing PersonaOptions in stepContext.State (key=persona).");

            // 2) profile（你原逻辑）
            var profile = stepContext.State.TryGetValue("profile", out var p) ? p as Dictionary<string, string> : null;

            // 3) working_memory（你原逻辑里需要它）
            var wm = stepContext.State.TryGetValue("working_memory", out var wmObj)
                ? wmObj as RunWorkingMemory
                : null;

            // 5) system：沿用你原来的规则（persona + profile + greeting/identity）
            var system = BuildSystem(persona, profile, stepContext.Input);
            // 产品级：Chat task（包含 user_input + step instruction + last_step）

            var userTask = BuildChatTask(stepContext, wm, run.UserInput);

            // 产品级：User prompt 由统一入口生成（会发 prompt_composed target=chat）
            var composed = _prep
                .GetPromptAsync(run, PromptTarget.Chat, userTask, 12000, stepContext.CancellationToken)
                .GetAwaiter().GetResult();

            return new ChatContext
            {
                SystemMessage = system,
                UserMessage = composed.User
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


        private static string BuildChatTask(AgentContext stepContext, RunWorkingMemory? wm, string userInput)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[User Input]");
            sb.AppendLine(userInput);

            sb.AppendLine();
            sb.AppendLine("[Current Step Instruction]");
            sb.AppendLine(stepContext.Input);

            if (wm?.LastStep != null)
            {
                sb.AppendLine();
                sb.AppendLine("[Last Step]");
                sb.AppendLine($"order={wm.LastStep.Order} kind={wm.LastStep.Kind} target={wm.LastStep.Target} success={wm.LastStep.Success}");
                if (!string.IsNullOrWhiteSpace(wm.LastStep.OutputPreview))
                    sb.AppendLine($"output: {wm.LastStep.OutputPreview}");
            }

            return sb.ToString().Trim();
        }

    }
}
