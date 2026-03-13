using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SKAgent.Core.Observability
{
    /// <summary>
    /// 运行期事件模型（统一事件封装）。
    /// 产品级推荐：不可变 + 结构化 payload（JsonElement）。
    /// </summary>
    public sealed record RunEvent(
    string RunId,
    DateTimeOffset TsUtc,
    long Seq,
    string Type,
    JsonElement Payload);

}
