using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Memory
{
    /// <summary>
    /// 统一记忆条目模型。
    /// 各记忆层（短期/工作/长期）在进入 Prompt 组装前会统一映射为该结构。
    /// </summary>
    public sealed record MemoryItem(
      string Id,
      MemoryLayer Layer,
      string Content,
      DateTimeOffset At,
      double? Score = null,
      IReadOnlyDictionary<string, string>? Metadata = null
  );
}
