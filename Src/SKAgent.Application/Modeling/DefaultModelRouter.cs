using SKAgent.Core.Modeling;

namespace SKAgent.Application.Modeling;

public sealed class DefaultModelRouter : IModelRouter
{
    public ModelSelection Select(ModelPurpose purpose)
    {
        return purpose switch
        {
            ModelPurpose.Planner => new ModelSelection(purpose, "openai-compatible", "gpt-4o-mini", "planner_low_latency"),
            ModelPurpose.Chat => new ModelSelection(purpose, "openai-compatible", "gpt-4o", "chat_quality"),
            ModelPurpose.Embedding => new ModelSelection(purpose, "local", "hash-embedding-v1-128", "offline_default"),
            _ => new ModelSelection(purpose, "openai-compatible", "gpt-4o-mini", "default")
        };
    }
}
