using SKAgent.Core.Modeling;

namespace SKAgent.Application.Modeling;

public sealed class DefaultModelRouter : IModelRouter
{
    private readonly ModelRoutingOptions _options;

    public DefaultModelRouter(ModelRoutingOptions options)
    {
        _options = options;
    }

    public ModelSelection Select(ModelPurpose purpose)
    {
        var configured = purpose switch
        {
            ModelPurpose.Planner => _options.Planner,
            ModelPurpose.Chat => _options.Chat,
            ModelPurpose.Daily => _options.Daily,
            ModelPurpose.Embedding => _options.Embedding,
            ModelPurpose.Rerank => _options.Rerank,
            ModelPurpose.VoiceStt => _options.VoiceStt,
            ModelPurpose.VoiceTts => _options.VoiceTts,
            _ => null
        };

        var fallback = purpose switch
        {
            ModelPurpose.Planner => new ModelSelection(purpose, "openai-compatible", "gpt-4o-mini", "planner_low_latency"),
            ModelPurpose.Chat => new ModelSelection(purpose, "openai-compatible", "gpt-4o", "chat_quality"),
            ModelPurpose.Daily => new ModelSelection(purpose, "openai-compatible", "gpt-4o-mini", "daily_balanced"),
            ModelPurpose.Embedding => new ModelSelection(purpose, "local", "hash-embedding-v1-128", "offline_default"),
            ModelPurpose.Rerank => new ModelSelection(purpose, "openai-compatible", "gpt-4o-mini", "rerank_default"),
            ModelPurpose.VoiceStt => new ModelSelection(purpose, "openai-compatible", "Systran/faster-whisper-small", "voice_stt_local_default"),
            ModelPurpose.VoiceTts => new ModelSelection(purpose, "kokoro-local", "tts-1", "voice_tts_local_default"),
            _ => new ModelSelection(purpose, "openai-compatible", "gpt-4o-mini", "default")
        };

        if (configured is null
            || string.IsNullOrWhiteSpace(configured.Provider)
            || string.IsNullOrWhiteSpace(configured.Model))
        {
            return fallback;
        }

        return new ModelSelection(
            purpose,
            configured.Provider.Trim(),
            configured.Model.Trim(),
            string.IsNullOrWhiteSpace(configured.Reason) ? fallback.Reason : configured.Reason.Trim());
    }
}
