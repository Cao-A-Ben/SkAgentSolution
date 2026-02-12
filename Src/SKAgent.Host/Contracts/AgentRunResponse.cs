namespace SKAgent.Host.Contracts
{
    public class AgentRunResponse
    {
        public string ConversationId { get; set; } = string.Empty;

        public string RunId { get; set;  } = string.Empty;

        public string Goal { get; init;  } = string.Empty;

        public string Status { get; init;  } = string.Empty;

        public string Output { get; init;  } = string.Empty;

        // ✅ 4.2：画像快照
        public Dictionary<string, string>? ProfileSnapshot { get; init; }

        // ✅ steps：强类型（别用 object/anonymous）
        public IReadOnlyList<AgentStepResponse> Steps { get; init; } = Array.Empty<AgentStepResponse>();
    }

    public sealed class AgentStepResponse
    {
        public int Order { get; init; }
        public string Agent { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Output { get; init; } = string.Empty;
        public string? Error { get; init; }
    }
}
