using Microsoft.AspNetCore.Mvc;
using SKAgent.Core.Personas;
using SKAgent.Host.Contracts.Personas;

namespace SKAgent.Host.Controllers;

[ApiController]
[Route("api/personas")]
public sealed class PersonasController : ControllerBase
{
    private readonly IPersonaProvider _provider;
    private readonly IConversationPersonaStore _conversationPersonaStore;

    public PersonasController(
        IPersonaProvider provider,
        IConversationPersonaStore conversationPersonaStore)
    {
        _provider = provider;
        _conversationPersonaStore = conversationPersonaStore;
    }

    [HttpGet]
    public IActionResult List()
    {
        var all = _provider.GetAll();
        var defaultPersonaName = ResolveDefaultPersonaName(all);

        var items = all
            .Select(persona => new PersonaSummaryResponse
            {
                Name = persona.Name,
                IsDefault = string.Equals(persona.Name, defaultPersonaName, StringComparison.OrdinalIgnoreCase),
                AllowSwitch = persona.Policy.AllowSwitch,
                PersistSelection = persona.Policy.PersistSelection
            })
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(items);
    }

    [HttpPost("current")]
    public IActionResult SetCurrent([FromBody] SetCurrentPersonaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
            return BadRequest(new { error = "conversationId is required." });

        if (string.IsNullOrWhiteSpace(request.PersonaName))
            return BadRequest(new { error = "personaName is required." });

        var requested = _provider.GetByName(request.PersonaName);
        if (requested is null)
            return BadRequest(new { error = $"Persona not found: {request.PersonaName}" });

        var persistedName = _conversationPersonaStore.Get(request.ConversationId);
        var persisted = !string.IsNullOrWhiteSpace(persistedName)
            ? _provider.GetByName(persistedName!)
            : null;

        if (persisted is not null
            && !persisted.Policy.AllowSwitch
            && !string.Equals(persisted.Name, requested.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                error = "Persona switch is not allowed for this conversation.",
                conversationId = request.ConversationId,
                personaName = persisted.Name
            });
        }

        var changed = !string.Equals(persisted?.Name, requested.Name, StringComparison.OrdinalIgnoreCase);
        var isPersisted = !string.IsNullOrWhiteSpace(persistedName);

        if (requested.Policy.PersistSelection)
        {
            if (changed || !isPersisted)
                _conversationPersonaStore.Set(request.ConversationId, requested.Name);

            isPersisted = true;
        }

        return Ok(new SetCurrentPersonaResponse
        {
            ConversationId = request.ConversationId,
            PersonaName = requested.Name,
            Source = "request",
            IsPersisted = isPersisted,
            Changed = changed
        });
    }

    [HttpGet("current")]
    public IActionResult Current([FromQuery] string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return BadRequest(new { error = "conversationId is required." });

        var all = _provider.GetAll();
        if (all.Count == 0)
            return NotFound(new { error = "No personas are configured." });

        var persistedName = _conversationPersonaStore.Get(conversationId);
        var persisted = !string.IsNullOrWhiteSpace(persistedName)
            ? _provider.GetByName(persistedName!)
            : null;

        if (persisted is not null)
        {
            return Ok(new CurrentPersonaResponse
            {
                ConversationId = conversationId,
                PersonaName = persisted.Name,
                Source = "store",
                IsPersisted = true
            });
        }

        var defaultPersonaName = ResolveDefaultPersonaName(all);
        return Ok(new CurrentPersonaResponse
        {
            ConversationId = conversationId,
            PersonaName = defaultPersonaName,
            Source = "default",
            IsPersisted = false
        });
    }

    private string ResolveDefaultPersonaName(IReadOnlyList<PersonaOptions> all)
    {
        var configuredDefault = all
            .Select(x => x.Policy.DefaultPersonaName)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return _provider.GetByName("default")?.Name
            ?? (configuredDefault is not null ? _provider.GetByName(configuredDefault)?.Name : null)
            ?? all.First().Name;
    }
}
