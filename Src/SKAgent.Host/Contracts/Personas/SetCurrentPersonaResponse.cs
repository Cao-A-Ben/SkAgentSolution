namespace SKAgent.Host.Contracts.Personas;

public sealed class SetCurrentPersonaResponse
{
    public string ConversationId { get; set; } = string.Empty;

    public string PersonaName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public bool IsPersisted { get; set; }

    public bool Changed { get; set; }
}
