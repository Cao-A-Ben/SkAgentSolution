namespace SKAgent.Host.Contracts.Personas;

public sealed class SetCurrentPersonaRequest
{
    public string ConversationId { get; init; } = string.Empty;

    public string PersonaName { get; init; } = string.Empty;
}
