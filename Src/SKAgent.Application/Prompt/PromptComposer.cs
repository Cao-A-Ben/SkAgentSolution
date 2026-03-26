using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Core.Memory;
using SKAgent.Core.Personas;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Prompt
{
    /// <summary>
    /// Week6 统一 Prompt 组合器。
    /// 负责将 persona + memory + task 组装为可直接发送给 LLM 的 prompt。
    /// </summary>
    public sealed class PromptComposer
    {
        /// <summary>
        /// 生成组合后的 Prompt，并发出 `prompt_composed` 观测事件。
        /// </summary>
        public async Task<ComposedPrompt> ComposeAsync(
            IRunContext run,
            PersonaOptions persona,
            MemoryBundle bundle,
            PromptTarget target,
            string taskOrUserMessage,
            int charBudget,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // 1) system
            var system = persona.SystemPrompt ?? string.Empty;

            if (target == PromptTarget.Planner && !string.IsNullOrWhiteSpace(persona.PlannerHint))
                system = system + "\n\n" + persona.PlannerHint;

            // 2) memory injection (W6-3 先用文本拼接，W6-4/7 再升级模板)
            var layersUsed = new List<string>();
            var memoryText = BuildMemoryText(bundle, layersUsed);

            // 3) user
            var user = string.IsNullOrWhiteSpace(memoryText)
                ? taskOrUserMessage
                : $"{memoryText}\n\nTASK:\n{taskOrUserMessage}";

            // 4) budget clip（W6-3：先按字符裁剪 user，system 保持完整）
            if (charBudget > 0 && user.Length > charBudget)
                user = user[^charBudget..];

            var hash = Sha256($"{target}|{system}|{user}");

            // 5) event
            // 注意：不要在 payload 放 runId（避免你之前那个 runId 不一致问题）
            await run.EmitAsync("prompt_composed", new
            {
                target = target.ToString().ToLowerInvariant(),
                hash,
                charBudget,
                layersUsed,
                systemChars = system.Length,
                userChars = user.Length
            }, ct);

            return new ComposedPrompt(target, system, user, hash, charBudget, layersUsed);
        }

        /// <summary>
        /// 按层拼接记忆文本并记录实际使用的层。
        /// </summary>
        private static string BuildMemoryText(MemoryBundle b, List<string> layersUsed)
        {
            var sb = new StringBuilder();

            void Add(string title, IReadOnlyList<MemoryItem> items, string layerName)
            {
                if (items.Count == 0) return;
                layersUsed.Add(layerName);

                sb.AppendLine($"[{title}]");
                foreach (var it in items)
                    sb.AppendLine($"- {it.Content}");
                sb.AppendLine();
            }

            Add("SHORT-TERM", b.ShortTerm, "short-term");
            Add("WORKING", b.Working, "working");
            Add("LONG-TERM", b.LongTerm, "long-term");

            return sb.ToString().Trim();
        }

        /// <summary>
        /// 计算 Prompt 内容哈希，用于回溯与去重分析。
        /// </summary>
        private static string Sha256(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
