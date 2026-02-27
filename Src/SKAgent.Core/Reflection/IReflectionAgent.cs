using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Planning;

namespace SKAgent.Core.Reflection
{
    public interface IReflectionAgent
    {
        Task<ReflectionDecision> DecideAsync(
            ReflectionContext run, 
            PlanStep step, 
            string failurePhase, // tool|agent|output_mismatch
            ErrorInfo error,
            int attempt,
            int maxRetries,
            //string reason, 
            CancellationToken ct);
    }


}
