using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Utilities
{
    /// <summary>
    /// 【Core 工具层 - LLM 输出解析器】
    /// 用于从 LLM 的原始文本输出中提取有效的 JSON 字符串。
    /// PlannerAgent 调用 LLM 生成执行计划后，LLM 的返回可能包含 Markdown 标记或
    /// 额外的说明文字，本工具负责从中安全地截取出 JSON 部分。
    /// </summary>
    public static class LlmOutputParser
    {
        /// <summary>
        /// 从 LLM 原始输出文本中提取第一个完整的 JSON 对象字符串。
        /// 由 PlannerAgent.CreatPlanAsync 在解析 LLM 返回的计划 JSON 时调用。
        /// </summary>
        /// <param name="text">LLM 的原始输出文本（可能包含额外说明或 Markdown 标记）。</param>
        /// <returns>提取到的 JSON 字符串（从第一个 '{' 到最后一个 '}'）。</returns>
        /// <exception cref="ArgumentException">当输入文本为空或空白时抛出。</exception>
        /// <exception cref="InvalidOperationException">当无法在文本中找到有效 JSON 结构时抛出。</exception>
        public static string ExtractJson(string text)
        {
            // 1. 校验输入不能为空
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("LLM output is empty.");

            // 2. 处理可能存在的双大括号包裹格式，例如 {{"a":1}} → {"a":1}
            var normalized = text.Replace("{{", "{").Replace("}}", "}");

            // 3. 定位第一个 '{' 和最后一个 '}' 的位置
            var start = normalized.IndexOf('{');
            var end = normalized.LastIndexOf('}');

            // 4. 校验是否找到合法的 JSON 边界
            if (start < 0 || end < 0 || end <= start)
                throw new InvalidOperationException(
                    $"No valid JSON found in LLM output: {text}");

            // 5. 截取并返回 JSON 字符串
            return normalized.Substring(start, end - start + 1);
        }
    }
}
