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
using SKAgent.Core.Retrieval;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Chat
{
    public sealed class DefaultChatContextComposer : IChatContextComposer
    {
        private readonly IRunPreparationService _prep;

        public DefaultChatContextComposer(IRunPreparationService prep)
        {
            _prep = prep;
        }

        public ChatContext Compose(AgentContext stepContext)
        {
            if (!stepContext.State.TryGetValue("run", out var ro) || ro is not IRunContext run)
                throw new InvalidOperationException("Missing IRunContext in stepContext.State (key=run).");

            if (!stepContext.State.TryGetValue("persona", out var po) || po is not PersonaOptions persona)
                throw new InvalidOperationException("Missing PersonaOptions in stepContext.State (key=persona).");

            var profile = stepContext.State.TryGetValue("profile", out var p)
                ? p as IReadOnlyDictionary<string, string>
                : null;

            var wm = stepContext.State.TryGetValue("working_memory", out var wmObj)
                ? wmObj as RunWorkingMemory
                : null;

            var intents = stepContext.State.TryGetValue("retrieval_intents", out var riObj) && riObj is RetrievalIntent retrievalIntents
                ? retrievalIntents
                : RetrievalIntent.None;

            var recallCandidate = stepContext.State.TryGetValue("recall_answer_candidate", out var rcObj)
                ? rcObj as string
                : null;

            var system = BuildSystem(persona, profile, run.UserInput, intents);
            var userTask = BuildChatTask(stepContext, wm, run.UserInput, intents, recallCandidate);

            var composed = _prep
                .GetPromptAsync(run, PromptTarget.Chat, userTask, 12000, stepContext.CancellationToken)
                .GetAwaiter().GetResult();

            return new ChatContext
            {
                SystemMessage = system,
                UserMessage = composed.User
            };
        }

        private static string BuildSystem(
            PersonaOptions persona,
            IReadOnlyDictionary<string, string>? profile,
            string input,
            RetrievalIntent intents)
        {
            var sb = new StringBuilder();
            sb.AppendLine(persona.SystemPrompt.Trim());

            if (profile is { Count: > 0 })
            {
                sb.AppendLine();
                sb.AppendLine("[User Profile]");
                foreach (var kvp in profile)
                    sb.AppendLine($"{kvp.Key}={kvp.Value}");
            }

            if (IsGreetingOrSmallTalk(input))
            {
                sb.AppendLine();
                sb.AppendLine("[Response Policy]");
                sb.AppendLine("当前为寒暄/建立关系场景：请用不超过2句话回复，并追加1个澄清问题。不要输出长列表。");
            }

            if (IsAskingIdentity(input))
            {
                sb.AppendLine();
                sb.AppendLine("[Critical Rule]");
                sb.AppendLine("如果用户询问“我是谁/我叫什么”，必须优先使用 User Profile 中的 name 字段作答。");
                sb.AppendLine("若 name 缺失或不确定，必须说明不知道并反问用户姓名；禁止杜撰或改写。");
            }

            if (intents.HasFlag(RetrievalIntent.Recall))
            {
                sb.AppendLine();
                sb.AppendLine("[Recall Policy]");
                sb.AppendLine("对于“我刚刚说了什么/我前面提到什么/我刚才让你做了什么”这类问题，优先使用提供的 Recall Answer Candidate 直接作答。");
                sb.AppendLine("如果提供了 Recall Answer Candidate，就把它视为优先答案草稿：先直接回答它，再按需要做极轻微自然化，不要改写成别的内容。");
                sb.AppendLine("默认先回答最近用户原话；如有必要，只补一句系统刚才做了什么。禁止把当前这句回忆问题本身当作答案。不要复述冗长旧回答、工具细节、编码过程或技术中间表示，除非用户明确追问。");
            }

            return sb.ToString();
        }

        private static bool IsGreetingOrSmallTalk(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return true;

            var t = input.Trim();
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
            return t.Contains("你是谁")
                || t.Contains("你叫什么")
                || t.Contains("我是谁")
                || t.Contains("我叫什么");
        }

        private static string BuildChatTask(AgentContext stepContext, RunWorkingMemory? wm, string userInput, RetrievalIntent intents, string? recallCandidate)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[User Input]");
            sb.AppendLine(userInput);

            if (intents.HasFlag(RetrievalIntent.Recall))
            {
                sb.AppendLine();
                sb.AppendLine("[Recall Response Mode]");
                sb.AppendLine("smart_summary");
                sb.AppendLine("先回答最近用户原话；如果用户在问系统刚才做了什么，再补一句动作摘要。默认不要复述长解释。");

                if (!string.IsNullOrWhiteSpace(recallCandidate))
                {
                    sb.AppendLine();
                    sb.AppendLine("[Recall Answer Candidate]");
                    sb.AppendLine(recallCandidate);
                    sb.AppendLine("如果这个候选已经足够回答问题，就直接输出它，最多只做非常轻微的语气调整。不要把当前这句回忆问题本身当作刚刚说的话，也不要把候选改写成别的历史内容。");
                }
            }

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
