using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SKAgent.Agents.Observability.Exporters
{
    public sealed class SseRunEventSink : IRunEventSink
    {
        private readonly HttpResponse _response;
        private readonly SemaphoreSlim _gate = new(1, 1);


        public SseRunEventSink(HttpResponse response)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));

            _response.ContentType = "text/event-stream";
            _response.Headers.Add("Cache-Control", "no-cache");
            _response.Headers.Add("Connection", "keep-alive");
        }

        public async ValueTask EmitAsync(Runevnet evt, CancellationToken ct)
        {
            // SSE 写入必须串行

            await _gate.WaitAsync(ct);

            try
            {
                await _response.WriteAsync($"event:{evt.Type}\n", ct);

                // 统一 envelope 前端可直接拿到 runId/seq/ts/type/payload）
                var envelope = new
                {
                    runId = evt.RunId,
                    seq = evt.Seq,
                    ts = evt.TsUtc.ToUnixTimeMilliseconds(),
                    type = evt.Type,
                    payload = evt.Payload
                };

                var json = JsonSerializer.Serialize(envelope);
                await _response.WriteAsync($"data:{json}\n\n", ct);
                await _response.Body.FlushAsync(ct);
            }
            finally
            {
                _gate.Release();
            }

        }
    }
}
