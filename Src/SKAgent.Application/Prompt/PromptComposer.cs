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

            var system = persona.SystemPrompt ?? string.Empty;

            if (target == PromptTarget.Planner && !string.IsNullOrWhiteSpace(persona.PlannerHint))
                system = system + "\n\n" + persona.PlannerHint;

            var layersUsed = new List<string>();
            var memoryText = BuildMemoryText(bundle, layersUsed);

            var user = string.IsNullOrWhiteSpace(memoryText)
                ? taskOrUserMessage
                : $"{memoryText}\n\nTASK:\n{taskOrUserMessage}";

            if (charBudget > 0 && user.Length > charBudget)
                user = user[^charBudget..];

            var hash = Sha256($"{target}|{system}|{user}");

            await run.EmitAsync("prompt_composed", new
            {
                target = target.ToString().ToLowerInvariant(),
                hash,
                charBudget,
                layersUsed,
                systemChars = system.Length,
                userChars = user.Length,
                systemText = system,
                userText = user
            }, ct);

            return new ComposedPrompt(target, system, user, hash, charBudget, layersUsed);
        }

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

            Add("RECENT-HISTORY", b.RecentHistory, "recent-history");
            Add("SHORT-TERM", b.ShortTerm, "short-term");
            Add("WORKING", b.Working, "working");
            Add("LONG-TERM", b.LongTerm, "long-term");

            return sb.ToString().Trim();
        }

        private static string Sha256(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
