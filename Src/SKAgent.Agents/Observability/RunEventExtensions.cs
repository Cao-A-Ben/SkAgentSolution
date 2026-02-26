using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using SKAgent.Agents.Runtime;

namespace SKAgent.Agents.Observability
{
    /// <summary>
    /// RunEvent 扩展方法，负责把 payload 序列化并投递到 EventSink。
    /// </summary>
    public static class RunEventExtensions
    {
        /// <summary>
        /// 发射事件到运行上下文的 EventSink。
        /// </summary>
        /// <param name="run">当前运行上下文。</param>
        /// <param name="type">事件类型。</param>
        /// <param name="payloadObj">事件负载对象。</param>
        /// <param name="ct">取消令牌。</param>
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
