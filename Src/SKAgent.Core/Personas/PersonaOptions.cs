namespace SKAgent.Core.Personas;

/// <summary>
/// 人格配置选项，用于约束系统提示词与规划提示词。
/// </summary>
public sealed class PersonaOptions
{

    public string Name { get; init; } = "default";

    public string SystemPrompt { get; init; } = string.Empty;

    public string PlannerHint { get; init; } = string.Empty;

    /// <summary>
    /// 人格策略：是否允许切换、是否持久化选择、默认人格名。
    /// </summary>
    public PersonaPolicy Policy { get; init; } = new();
}
