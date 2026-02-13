using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Observability
{
    public interface IRunEventSink
    {
        ValueTask EmitAsync(Runevnet evt, CancellationToken ct);
    }
}
