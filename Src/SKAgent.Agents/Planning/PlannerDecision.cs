using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Planning
{
    public sealed class PlannerDecision
    {
        /// <summary>
        /// Gets or sets the agent.
        /// </summary>
        /// <value>
        /// The agent.
        /// </value>
        public string Agent { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the reason.
        /// </summary>
        /// <value>
        /// The reason.
        /// </value>
        public string? Reason { get; set; }
    }
}
