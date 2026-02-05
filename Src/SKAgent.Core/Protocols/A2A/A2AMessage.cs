using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Core.Protocols.A2A
{
    public sealed record A2AMessage(string Agent, string Payload);
}
