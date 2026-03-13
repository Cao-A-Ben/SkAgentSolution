using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SKAgent.Core.Memory;

namespace SKAgent.Application.Memory
{
    /// <summary>
    /// Week6 记忆预算裁剪器。
    /// 负责对候选记忆进行去重与字符预算裁剪，避免注入 Prompt 时上下文过长。
    /// </summary>
    public sealed class MemoryBudgeter
    {
        /// <summary>
        /// 按字符预算裁剪记忆条目，并返回裁剪原因。
        /// </summary>
        public IReadOnlyList<MemoryItem> ClipByChars(
            IReadOnlyList<MemoryItem> items,
            int budgetChars,
            out string truncateReason)
        {
            truncateReason = "none";
            if (budgetChars <= 0 || items.Count == 0) return Array.Empty<MemoryItem>();

            // 去重：同 Content hash 去重（保持顺序：先最新后最旧由调用方保证）
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var deduped = new List<MemoryItem>(items.Count);

            foreach (var it in items)
            {
                var key = Sha1(it.Content ?? string.Empty);
                if (seen.Add(key))
                    deduped.Add(it);
            }

            if (deduped.Count != items.Count)
                truncateReason = "deduped";

            // 裁剪：从前往后累加直到超预算（假设 items 已按“最新在前”）
            var acc = 0;
            var result = new List<MemoryItem>(deduped.Count);
            foreach (var it in deduped)
            {
                var len = (it.Content ?? string.Empty).Length;
                if (acc + len > budgetChars)
                {
                    truncateReason = truncateReason == "none" ? "budget_exceeded" : truncateReason + "+budget_exceeded";
                    break;
                }
                result.Add(it);
                acc += len;
            }

            return result;
        }

        /// <summary>
        /// 计算文本内容的 SHA1，用于去重键。
        /// </summary>
        private static string Sha1(string s)
        {
            using var sha = SHA1.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes);
        }
    }
}
