using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Runtime;

namespace SKAgent.Core.Reflection
{
    public sealed record ReflectionContext(
      string RunId,
      string ConversationId,
      string UserInput,
      string? PersonaId,
      // 你已经在 working memory 里有这些快照：给 reflection 足够信息即可
      StepSnapshot? LastStep,
      ToolSnapshot? LastTool,
      IReadOnlyDictionary<int, int> RetryCounts,
      IReadOnlyDictionary<string, object>? ConversationStatePreview=null // 可选：只放白名单key
  );

}
