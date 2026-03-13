using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Runtime;

namespace SKAgent.Core.Planning
{
    public interface IPlanRequestFactory
    {
        IPlanner.PlanRequest Create(IRunContext run);
    }
}
