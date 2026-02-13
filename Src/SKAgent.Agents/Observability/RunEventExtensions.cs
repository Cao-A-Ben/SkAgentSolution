using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using SKAgent.Agents.Runtime;

namespace SKAgent.Agents.Observability
{
    public static class RunEventExtensions
    {
        public static async ValueTask EmitAsync(
            this AgentRunContext run,
            string type,
            object payloadObj,
             CancellationToken ct)
        {
            var json = JsonSerializer.SerializeToElement(payloadObj);

            var evt = new Runevnet(
                RunId: run.Root.RequestId,
                TsUtc: DateTimeOffset.UtcNow,
                Seq: run.NextEventSeq(),
                Type: type,
                Payload: json);


            await run.EventSink.EmitAsync(evt, ct).ConfigureAwait(false);
        }
    }
}
