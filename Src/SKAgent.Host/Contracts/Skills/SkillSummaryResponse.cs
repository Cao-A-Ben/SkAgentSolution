namespace SKAgent.Host.Contracts.Skills;

public sealed class SkillSummaryResponse
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string[] RecommendedTools { get; set; } = [];
}
