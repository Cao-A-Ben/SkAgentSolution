using Microsoft.AspNetCore.Mvc;
using SKAgent.Core.Personas;
using SKAgent.Host.Contracts.Personas;
using SKAgent.Host.Controllers;
using Xunit;

namespace SKAgent.Tests.Personas;

public sealed class PersonasControllerTests
{
    [Fact]
    public void List_ShouldReturnDefaultAndCoach()
    {
        var controller = new PersonasController(new TestPersonaProvider(), new TestConversationPersonaStore());

        var result = Assert.IsType<OkObjectResult>(controller.List());
        var payload = Assert.IsAssignableFrom<IEnumerable<PersonaSummaryResponse>>(result.Value);
        var items = payload.ToList();

        Assert.Equal(3, items.Count);
        Assert.Contains(items, x => x.Name == "default" && x.IsDefault);
        Assert.Contains(items, x => x.Name == "coach" && !x.IsDefault);
    }

    [Fact]
    public void Current_ShouldReturnDefault_WhenConversationIsNotPersisted()
    {
        var controller = new PersonasController(new TestPersonaProvider(), new TestConversationPersonaStore());

        var result = Assert.IsType<OkObjectResult>(controller.Current("conv-new"));
        var payload = Assert.IsType<CurrentPersonaResponse>(result.Value);

        Assert.Equal("conv-new", payload.ConversationId);
        Assert.Equal("default", payload.PersonaName);
        Assert.Equal("default", payload.Source);
        Assert.False(payload.IsPersisted);
    }

    [Fact]
    public void SetCurrent_ShouldPersistPersona_WhenPersonaExists()
    {
        var store = new TestConversationPersonaStore();
        var controller = new PersonasController(new TestPersonaProvider(), store);

        var result = Assert.IsType<OkObjectResult>(controller.SetCurrent(new SetCurrentPersonaRequest
        {
            ConversationId = "conv-1",
            PersonaName = "coach"
        }));

        var payload = Assert.IsType<SetCurrentPersonaResponse>(result.Value);
        Assert.Equal("coach", payload.PersonaName);
        Assert.True(payload.Changed);
        Assert.True(payload.IsPersisted);
        Assert.Equal("coach", store.Get("conv-1"));
    }

    [Fact]
    public void SetCurrent_ShouldBeIdempotent_WhenPersonaMatchesCurrentBinding()
    {
        var store = new TestConversationPersonaStore();
        store.Set("conv-1", "default");
        var controller = new PersonasController(new TestPersonaProvider(), store);

        var result = Assert.IsType<OkObjectResult>(controller.SetCurrent(new SetCurrentPersonaRequest
        {
            ConversationId = "conv-1",
            PersonaName = "default"
        }));

        var payload = Assert.IsType<SetCurrentPersonaResponse>(result.Value);
        Assert.False(payload.Changed);
        Assert.True(payload.IsPersisted);
        Assert.Equal("default", store.Get("conv-1"));
    }

    [Fact]
    public void SetCurrent_ShouldRejectUnknownPersona()
    {
        var controller = new PersonasController(new TestPersonaProvider(), new TestConversationPersonaStore());

        var result = Assert.IsType<BadRequestObjectResult>(controller.SetCurrent(new SetCurrentPersonaRequest
        {
            ConversationId = "conv-1",
            PersonaName = "missing"
        }));

        Assert.NotNull(result.Value);
    }

    [Fact]
    public void SetCurrent_ShouldRejectSwitch_WhenPersistedPersonaDisallowsSwitch()
    {
        var store = new TestConversationPersonaStore();
        store.Set("conv-locked", "locked");
        var controller = new PersonasController(new TestPersonaProvider(), store);

        var result = Assert.IsType<ConflictObjectResult>(controller.SetCurrent(new SetCurrentPersonaRequest
        {
            ConversationId = "conv-locked",
            PersonaName = "coach"
        }));

        Assert.NotNull(result.Value);
        Assert.Equal("locked", store.Get("conv-locked"));
    }

    private sealed class TestPersonaProvider : IPersonaProvider
    {
        private readonly PersonaOptions[] _all =
        [
            new()
            {
                Name = "default",
                SystemPrompt = "You are helpful.",
                PlannerHint = string.Empty,
                Policy = new PersonaPolicy { DefaultPersonaName = "default", PersistSelection = true, AllowSwitch = true }
            },
            new()
            {
                Name = "coach",
                SystemPrompt = "You are a coaching assistant.",
                PlannerHint = string.Empty,
                Policy = new PersonaPolicy { DefaultPersonaName = "default", PersistSelection = true, AllowSwitch = true }
            },
            new()
            {
                Name = "locked",
                SystemPrompt = "You are locked.",
                PlannerHint = string.Empty,
                Policy = new PersonaPolicy { DefaultPersonaName = "default", PersistSelection = true, AllowSwitch = false }
            }
        ];

        public IReadOnlyList<PersonaOptions> GetAll() => _all;

        public PersonaOptions? GetByName(string name)
            => _all.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TestConversationPersonaStore : IConversationPersonaStore
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

        public string? Get(string conversationId)
            => _store.TryGetValue(conversationId, out var personaName) ? personaName : null;

        public void Set(string conversationId, string personaName)
            => _store[conversationId] = personaName;
    }
}
