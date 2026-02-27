namespace SKAgent.Core.Personas;

/// <summary>
/// 人格定义，包含配置与可选策略。
/// </summary>
public sealed class Persona
{
    public required PersonaOptions Options { get; init; }

    public PersonaPolicy? Policy { get; init; }
}
