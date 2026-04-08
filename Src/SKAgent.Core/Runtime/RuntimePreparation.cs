using System;
using System.Collections.Generic;
using System.Text;
using SkAgent.Core.Prompt;
using SKAgent.Core.Memory;
using SKAgent.Core.Personas;

namespace SKAgent.Core.Runtime
{
    public sealed class RuntimePreparation
    {
        public PersonaOptions Persona { get; set; } = default!;
        public MemoryBundle Memory { get; set; } = new(
            Array.Empty<MemoryItem>(),
            Array.Empty<MemoryItem>(),
            Array.Empty<MemoryItem>(),
            Array.Empty<MemoryItem>());

        public Dictionary<string, ComposedPrompt> PromptCache { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

}
