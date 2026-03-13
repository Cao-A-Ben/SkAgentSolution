using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Memory
{
    /// <summary>
    /// 记忆层级枚举。
    /// </summary>
    public enum MemoryLayer
    {
        /// <summary>会话短期记忆（近期对话回合）。</summary>
        ShortTerm = 1,

        /// <summary>工作记忆（运行中的中间事实）。</summary>
        Working = 2,

        /// <summary>长期记忆（跨会话持久知识）。</summary>
        LongTerm = 3
    }
}
