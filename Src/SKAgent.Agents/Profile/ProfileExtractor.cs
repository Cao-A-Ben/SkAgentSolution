using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SKAgent.Agents.Profile
{
    /// <summary>
    /// 【Profile 层 - 画像提取器】
    /// 从用户原始输入中提取画像字段（如姓名、地点、作息习惯）。
    /// 当前为规则版实现（正则 + 关键词匹配），后续可升级为 LLM 驱动的智能提取。
    /// 
    /// 在运行时流程中的使用：
    /// AgentRuntimeService.RunAsync 在回合结束后调用 ExtractPath，
    /// 将提取到的 patch 通过 IUserProfileStore.UpsertAsync 写入存储。
    /// </summary>
    public static class ProfileExtractor
    {
        /// <summary>
        /// 从用户输入文本中提取画像字段（增量 patch）。
        /// 返回的字典仅包含本次新提取到的字段，若未匹配到任何规则则返回空字典。
        /// </summary>
        /// <param name="userInput">用户原始输入文本。</param>
        /// <returns>提取到的画像字段字典，常见 key: name / location / sleep。</returns>
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
