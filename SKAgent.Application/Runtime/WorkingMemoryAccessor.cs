using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Runtime;

namespace SKAgent.Application.Runtime
{
    public static class WorkingMemoryAccessor
    {
        public static RunWorkingMemory GetOrCreate(AgentRunContext run)
        {
            if (run.ConversationState.TryGetValue("working_memory", out var wmObj) &&
                wmObj is RunWorkingMemory wm)
            {
                return wm;
            }

            wm = new RunWorkingMemory();
            run.ConversationState["working_memory"] = wm;
            return wm;
        }
    }
}
