namespace SKAgent.Core.Memory.LongTerm;

/// <summary>
/// 长期记忆写入请求。
/// </summary>
public sealed record LongTermMemoryWrite(
    string ConversationId,
    string RunId,
    string Persona,
    DateTimeOffset Ts,
    string Content,
    IReadOnlyDictionary<string, string>? Metadata = null
);

/// <summary>
/// 长期记忆写入结果。
/// </summary>
public sealed record LongTermUpsertResult(
    int Inserted,
    int DedupeCount
);
