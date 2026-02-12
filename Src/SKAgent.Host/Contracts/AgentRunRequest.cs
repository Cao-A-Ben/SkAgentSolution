namespace SKAgent.Host.Contracts
{
    public class AgentRunRequest
    {
        public string? ConversationId { get; set; }

        public string Input { get; init; } = string.Empty;


    }


}
