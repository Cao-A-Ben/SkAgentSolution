using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Utilities
{
    public static class LlmOutputParser
    {
        public static string ExtractJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("LLM output is empty.");

            // 处理可能存在的双大括号包裹格式，例如 {{"a":1}}
            var normalized = text.Replace("{{", "{").Replace("}}", "}");

            var start = normalized.IndexOf('{');
            var end = normalized.LastIndexOf('}');

            if (start < 0 || end < 0 || end <= start)
                throw new InvalidOperationException(
                    $"No valid JSON found in LLM output: {text}");

            // 返回提取到的 JSON 字符串
            return normalized.Substring(start, end - start + 1);
        }
    }
}
