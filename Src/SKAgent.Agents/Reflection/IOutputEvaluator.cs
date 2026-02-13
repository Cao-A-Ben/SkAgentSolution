using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Reflection
{
    public  interface IOutputEvaluator
    {
        bool IsSatisfied(string? output, string? expectedOutput);
    }


}
