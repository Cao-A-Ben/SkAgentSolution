namespace SKAgent.Core.Skills;

public interface ISkillRegistry
{
    IReadOnlyList<RuntimeSkillDefinition> List();

    RuntimeSkillDefinition? GetByName(string name);
}
