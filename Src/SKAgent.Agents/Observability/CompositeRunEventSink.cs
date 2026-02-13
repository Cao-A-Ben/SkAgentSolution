using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Observability
{
    public sealed class CompositeRunEventSink: IRunEventSink
    {
        private readonly IRunEventSink[] _sinks;
        public CompositeRunEventSink(params IRunEventSink[] sinks)
        {
            _sinks = sinks;
        }
        public async ValueTask EmitAsync(Runevnet evt, CancellationToken ct)
        {
            foreach (var sink in _sinks)
            {
                await sink.EmitAsync(evt, ct).ConfigureAwait(false);
            }
        }
    }
}
