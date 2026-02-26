using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Runtime
{
    /// <summary>
    /// 工作记忆辅助方法，负责获取/初始化 RunWorkingMemory 与生成文本预览。
    /// </summary>
    public static class WorkingMemoryHelper
    {
        /// <summary>
        /// 从运行上下文中获取工作记忆；若不存在则创建并写回。
        /// </summary>
        /// <param name="run">当前运行上下文。</param>
        /// <returns>运行期工作记忆实例。</returns>
        public static RunWorkingMemory GetOrCreateWorkingMemory(AgentRunContext run)
        {
            if (run.ConversationState.TryGetValue("working_memory", out var wmObj) && wmObj is RunWorkingMemory wm)
            {
                return wm;
            }

            wm = new RunWorkingMemory();

            run.ConversationState["working_memory"] = wm;

            return wm;
        }

        /// <summary>
        /// 生成文本预览，超过上限则截断追加省略号。
        /// </summary>
        /// <param name="s">原始字符串。</param>
        /// <param name="max">最大长度。</param>
        /// <returns>截断后的预览文本。</returns>
        public static string Preview(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }


}
