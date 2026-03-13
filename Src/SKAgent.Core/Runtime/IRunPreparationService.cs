using System;
using System.Collections.Generic;
using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Core.Runtime;

namespace SKAgent.Core.Runtime
{
    public interface IRunPreparationService
    {
        Task PrepareAsync(IRunContext run, CancellationToken ct);
        Task<ComposedPrompt> GetPromptAsync(IRunContext run, PromptTarget target, string task, int charBudget, CancellationToken ct);
    }

}
