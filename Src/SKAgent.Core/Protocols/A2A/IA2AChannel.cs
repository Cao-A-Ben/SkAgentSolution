using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.A2A
{
    public interface IA2AChannel
    {
        Task<string> SendAsync(A2AMessage message, CancellationToken cancellationToken = default);
    }
}
