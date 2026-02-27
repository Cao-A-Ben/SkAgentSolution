namespace SKAgent.Core.Personas;

/// <summary>
/// Persona 提供器契约。
/// </summary>
public interface IPersonaProvider
{
    IReadOnlyList<PersonaOptions> GetAll();

    PersonaOptions? GetByName(string name);
}
