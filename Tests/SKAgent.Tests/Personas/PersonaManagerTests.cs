using SKAgent.Application.Persona;
using SKAgent.Core.Personas;
using Xunit;

namespace SKAgent.Tests.Personas;

public sealed class PersonaManagerTests
{
    [Fact]
    public void GetOrSelect_ShouldUseRequestedPersona_WhenProvided()
    {
        var store = new TestConversationPersonaStore();
        var manager = new PersonaManager(new TestPersonaProvider(), store);

        var result = manager.GetOrSelect("run-1", "conv-1", "coach");

        Assert.Equal("coach", result.Persona.Name);
        Assert.Equal("request", result.Source);
        Assert.Equal("coach", store.Get("conv-1"));
    }

    [Fact]
    public void GetOrSelect_ShouldFallbackToDefault_ForNewConversationWithoutRequest()
    {
        var store = new TestConversationPersonaStore();
        var manager = new PersonaManager(new TestPersonaProvider(), store);

        var result = manager.GetOrSelect("run-1", "conv-new", null);

        Assert.Equal("default", result.Persona.Name);
        Assert.Equal("default", result.Source);
        Assert.Equal("default", store.Get("conv-new"));
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
