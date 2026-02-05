using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Runtime
{
    public enum AgentRunStatus
    {
        Initialized,
        Planned,
        Executing,
        Completed,
        Failed
    }
}
