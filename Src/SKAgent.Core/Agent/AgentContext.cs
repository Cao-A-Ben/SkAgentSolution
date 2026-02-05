using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Agent
{
    public sealed class AgentContext
    {

        public Guid RequestId { get; } = Guid.NewGuid();
        /// <summary>
        /// Gets the input.
        /// </summary>
        /// <value>
        /// The input.
        /// </value>
        public string Input { get; init; }=string.Empty;

        /// <summary>
        /// Gets the state.
        /// </summary>
        /// <value>
        /// The state.
        /// </value>
        public Dictionary<string, object> State { get; } = new();
        /// <summary>
        /// Gets the cancellation token.
        /// </summary>
        /// <value>
        /// The cancellation token.
        /// </value>
        public CancellationToken CancellationToken { get; init; }
    }
}
