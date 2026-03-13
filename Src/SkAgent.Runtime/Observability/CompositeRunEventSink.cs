using System;
using System.Collections.Generic;
using System.Text;
using SKAgent.Core.Observability;

namespace SKAgent.Application.Observability
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
        public async ValueTask WriteAsync(RunEvent evt, CancellationToken ct)
        {
            foreach (var sink in _sinks)
            {
                await sink.WriteAsync(evt, ct).ConfigureAwait(false);
            }
        }
    }
}
