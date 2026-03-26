namespace SKAgent.Core.Retrieval;

[Flags]
public enum RetrievalIntent
{
    None = 0,
    Chitchat = 1 << 0,
    Recall = 1 << 1,
    ToolNeeded = 1 << 2,
    HealthSensitive = 1 << 3,
    PreferenceUpdate = 1 << 4
}
