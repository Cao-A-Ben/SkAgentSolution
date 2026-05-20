using SKAgent.Core.Skills;

namespace SKAgent.Application.Skills;

public sealed class InMemorySkillRegistry : ISkillRegistry
{
    private readonly Dictionary<string, RuntimeSkillDefinition> _skills;

    public InMemorySkillRegistry(IEnumerable<RuntimeSkillDefinition> skills)
    {
        _skills = skills.ToDictionary(skill => skill.Name, StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeSkillDefinition? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _skills.TryGetValue(name.Trim(), out var skill) ? skill : null;
    }

    public IReadOnlyList<RuntimeSkillDefinition> List()
        => [.. _skills.Values.OrderBy(skill => skill.DisplayName, StringComparer.OrdinalIgnoreCase)];
}
