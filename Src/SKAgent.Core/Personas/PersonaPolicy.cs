namespace SKAgent.Core.Personas;

/// <summary>
/// 人格策略，用于声明选择与切换约束。
/// </summary>
public sealed class PersonaPolicy
{
    public bool AllowSwitch { get; init; } = true;

    public bool PersistSelection { get; init; } = true;

    public string? DefaultPersonaName { get; init; }
}
