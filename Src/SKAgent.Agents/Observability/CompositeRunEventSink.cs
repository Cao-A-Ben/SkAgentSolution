using System;
using System.Collections.Generic;
using System.Text;

namespace SKAgent.Agents.Observability
{
    /// <summary>
    /// 组合事件接收器，将事件顺序转发到多个下游 Sink。
    /// </summary>
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
