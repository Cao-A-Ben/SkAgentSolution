using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SKAgent.Agents.Observability
{
    /// <summary>
    /// 运行期事件模型（统一事件封装）。
    /// </summary>
    public sealed record Runevnet(
        string RunId,
        DateTimeOffset TsUtc,
        long Seq,
        string Type,
        JsonElement Payload);
}
