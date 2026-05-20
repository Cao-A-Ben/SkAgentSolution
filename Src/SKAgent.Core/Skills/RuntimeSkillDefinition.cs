namespace SKAgent.Core.Skills;

public sealed record RuntimeSkillDefinition(
    string Name,
    string DisplayName,
    string Description,
    string PlannerHint,
    string? SystemPromptAppendix = null,
    IReadOnlyList<string>? RecommendedTools = null);
