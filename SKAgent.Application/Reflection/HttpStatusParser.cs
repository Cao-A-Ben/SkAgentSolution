using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SKAgent.Application.Reflection
{
    public static class HttpStatusParser
    {
        // 从类似 "HTTP 400 (...)" 或 "status=429" 等字符串里提取状态码
        public static int? TryParseHttpStatus(string? message)
        {
            if (string.IsNullOrWhiteSpace(message)) return null;

            // 常见格式：HTTP 400
            var m1 = Regex.Match(message, @"\bHTTP\s+(?<code>\d{3})\b", RegexOptions.IgnoreCase);
            if (m1.Success && int.TryParse(m1.Groups["code"].Value, out var c1)) return c1;

            // 常见格式：status=400 或 status: 400
            var m2 = Regex.Match(message, @"\bstatus\s*[:=]\s*(?<code>\d{3})\b", RegexOptions.IgnoreCase);
            if (m2.Success && int.TryParse(m2.Groups["code"].Value, out var c2)) return c2;

            return null;
        }
    }
}
