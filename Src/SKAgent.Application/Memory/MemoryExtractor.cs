using SKAgent.Core.Memory.LongTerm;

namespace SKAgent.Application.Memory;

/// <summary>
/// 从运行事实提取长期记忆候选。
/// </summary>
public sealed class MemoryExtractor
{
    public IReadOnlyList<LongTermMemoryWrite> Extract(
        string conversationId,
        string runId,
        string userInput,
        string? finalOutput,
        string personaName)
    {
        var list = new List<LongTermMemoryWrite>();
        var ts = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(userInput))
        {
            list.Add(new LongTermMemoryWrite(
                ConversationId: conversationId,
                RunId: runId,
                Persona: personaName,
                Ts: ts,
                Content: $"[user] {userInput}",
                Metadata: new Dictionary<string, string>
                {
                    ["source"] = "user_input",
                    ["role"] = "user",
                    ["conversationId"] = conversationId,
                    ["runId"] = runId,
                    ["persona"] = personaName
                }));
        }

        if (!string.IsNullOrWhiteSpace(finalOutput))
        {
            list.Add(new LongTermMemoryWrite(
                ConversationId: conversationId,
                RunId: runId,
                Persona: personaName,
                Ts: ts,
                Content: $"[assistant] {finalOutput}",
                Metadata: new Dictionary<string, string>
                {
                    ["source"] = "assistant_output",
                    ["role"] = "assistant",
                    ["conversationId"] = conversationId,
                    ["runId"] = runId,
                    ["persona"] = personaName
                }));
        }

        return list;
    }
}
