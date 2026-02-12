using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SKAgent.Agents.Profile
{
    public static class ProfileExtractor
    {
        public static Dictionary<string, string> ExtractPath(string userInput)
        {
            var patch = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 极简规则： 识别  “我叫 X”
            var m = Regex.Match(userInput, @"我(叫|是)\s*(?<name>[\p{L}\p{N}_-]{1,20})");
            if (m.Success) patch["name"] = m.Groups["name"].Value;

            // 识别地点：新加坡
            if (userInput.Contains("新加坡", StringComparison.OrdinalIgnoreCase))
                patch["location"] = "新加坡";

            // 识别作息：晚睡（示例）
            if (userInput.Contains("11点", StringComparison.OrdinalIgnoreCase) || userInput.Contains("晚睡", StringComparison.OrdinalIgnoreCase))
                patch["sleep"] = "late";

            return patch;
        }
    }
}
