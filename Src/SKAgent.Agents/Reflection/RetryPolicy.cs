using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Reflection
{
    public sealed record RetryPolicy(
        int MaxRetriesPerStep = 2,
        int MaxReplansPerRun = 1
        );
}
