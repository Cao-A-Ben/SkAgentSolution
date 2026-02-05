using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Agent
{
    public interface IAgent
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        string Name { get; }

        /// <summary>
        /// Executes the asynchronous.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        Task<AgentResult> ExecuteAsync(AgentContext context );
    }
}
