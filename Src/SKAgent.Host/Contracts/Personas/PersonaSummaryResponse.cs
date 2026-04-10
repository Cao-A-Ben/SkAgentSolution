namespace SKAgent.Host.Contracts.Personas;

public sealed class PersonaSummaryResponse
{
    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool AllowSwitch { get; set; }

    public bool PersistSelection { get; set; }
}
