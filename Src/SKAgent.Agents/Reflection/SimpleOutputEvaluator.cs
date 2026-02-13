using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Reflection
{
    public class SimpleOutputEvaluator : IOutputEvaluator
    {


        public bool IsSatisfied(string? output, string? expectedOutput)
        {
            if (string.IsNullOrWhiteSpace(expectedOutput)) return true;

            if (string.IsNullOrWhiteSpace(output)) return false;

            return output.Contains(expectedOutput, StringComparison.OrdinalIgnoreCase);

        }
    }
}
