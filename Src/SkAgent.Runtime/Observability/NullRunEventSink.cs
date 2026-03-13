using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Observability;

namespace SKAgent.Application.Observability
{
    public sealed class NullRunEventSink : IRunEventSink
    {

        public static readonly NullRunEventSink Instance = new();


        public ValueTask WriteAsync(RunEvent evt, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }
}
