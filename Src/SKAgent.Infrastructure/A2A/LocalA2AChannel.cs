using System;
using System.Collections.Generic;
using System.Text;

using SKAgent.Core.Protocols.A2A;

namespace SKAgent.Infrastructure.A2A
{
    public class LocalA2AChannel : IA2AChannel
    {
        public Task<string> SendAsync(A2AMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"[A2A:{message.Agent}] {message.Payload}");
        }
    }
}
