using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Observability
{
    public sealed class NullRunEventSink : IRunEventSink
    {

        public static readonly NullRunEventSink Instance = new();


        public ValueTask EmitAsync(Runevnet evt, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }
}
