using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Tools
{
    public interface IAgentTool
    {
        string Name { get; }

        Task<string> ExecuteAsync(string input, CancellationToken cancellationToken = default);
    }
}
