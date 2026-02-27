using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Agent;

namespace SKAgent.Core.Routing
{
    public interface IStepRouter
    {
        Task<AgentResult> RouteAsync(AgentContext stepContext, CancellationToken ct = default);
    }
}
