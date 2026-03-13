using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Memory
{
    public sealed record MemoryQuery(
      string ConversationId,
      string Text,
      int TopK = 8,
      int? BudgetChars = null
  );
}
