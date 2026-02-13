using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SKAgent.Agents.Observability
{

    public sealed record Runevnet(
        string RunId,
        DateTimeOffset TsUtc,
        long Seq,
        string Type,
        JsonElement Payload);
}
